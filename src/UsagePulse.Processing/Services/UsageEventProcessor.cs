using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Options;
using UsagePulse.Processing.Services.Pipeline;

namespace UsagePulse.Processing.Services;

public sealed class UsageEventProcessor : IUsageEventProcessor
{
    private readonly IUsageEventValidationStage validationStage;
    private readonly IUsageEventDeduplicationStage deduplicationStage;
    private readonly IUsageEventPersistenceStage persistenceStage;
    private readonly IUsageEventAnalyticsStage analyticsStage;
    private readonly IUsageEventFinalizeStage finalizeStage;
    private readonly IDeadLetterSink deadLetterSink;
    private readonly ILogger<UsageEventProcessor> logger;
    private readonly ResiliencePipeline resiliencePipeline;

    public UsageEventProcessor(
        IUsageEventValidationStage validationStage,
        IUsageEventDeduplicationStage deduplicationStage,
        IUsageEventPersistenceStage persistenceStage,
        IUsageEventAnalyticsStage analyticsStage,
        IUsageEventFinalizeStage finalizeStage,
        IDeadLetterSink deadLetterSink,
        IOptions<UsagePulsePipelineOptions> options,
        ILogger<UsageEventProcessor> logger)
    {
        this.validationStage = validationStage;
        this.deduplicationStage = deduplicationStage;
        this.persistenceStage = persistenceStage;
        this.analyticsStage = analyticsStage;
        this.finalizeStage = finalizeStage;
        this.deadLetterSink = deadLetterSink;
        this.logger = logger;
        resiliencePipeline = CreatePipeline(options.Value, logger);
    }

    public async Task<ProcessingResult> ProcessAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        using var activity = StartActivity(usageEvent);

        var validation = validationStage.Validate(usageEvent);
        if (!validation.IsValid)
        {
            var failure = validation.Failure ?? new ProcessingFailure(DeadLetterReasonCode.ValidationFailed, "Validation failed.");
            logger.LogWarning("Rejected event {EventId}. ReasonCode={ReasonCode} ValidationCode={ValidationCode} Message={Message}", usageEvent.EventId, failure.ReasonCode, failure.ValidationCode, failure.Message);
            await deadLetterSink.PublishAsync(usageEvent, failure, cancellationToken);
            RecordMetric(usageEvent, "validation_failed", failure.ReasonCode);
            return ProcessingResult.Failure(0, failure.ReasonCode, failure.Message, failure.ValidationCode);
        }

        if (!await deduplicationStage.TryStartAsync(usageEvent, cancellationToken))
        {
            logger.LogInformation("Skipped duplicate event {EventId} for tenant {TenantId}.", usageEvent.EventId, usageEvent.TenantId);
            RecordMetric(usageEvent, "duplicate", null);
            return ProcessingResult.Duplicate();
        }

        var attempts = 0;
        try
        {
            await resiliencePipeline.ExecuteAsync(async ct =>
            {
                attempts++;
                await persistenceStage.PersistAsync(usageEvent, ct);
                await analyticsStage.ExportAsync(usageEvent, ct);
            }, cancellationToken);

            await finalizeStage.FinalizeAsync(usageEvent, cancellationToken);
            RecordMetric(usageEvent, "success", null);
            return ProcessingResult.Success(attempts);
        }
        catch (BrokenCircuitException ex)
        {
            var failure = new ProcessingFailure(DeadLetterReasonCode.CircuitOpen, ex.Message);
            logger.LogError(ex, "Circuit open while processing event {EventId}.", usageEvent.EventId);
            await deadLetterSink.PublishAsync(usageEvent, failure, cancellationToken);
            RecordMetric(usageEvent, "circuit_open", failure.ReasonCode);
            return ProcessingResult.Failure(attempts, failure.ReasonCode, failure.Message);
        }
        catch (Exception ex)
        {
            var failure = new ProcessingFailure(DeadLetterReasonCode.ProcessingFailed, ex.Message);
            logger.LogError(ex, "Processing failed for event {EventId} after {Attempts} attempts.", usageEvent.EventId, attempts);
            await deadLetterSink.PublishAsync(usageEvent, failure, cancellationToken);
            RecordMetric(usageEvent, "failed", failure.ReasonCode);
            return ProcessingResult.Failure(attempts, failure.ReasonCode, failure.Message);
        }
    }

    private static ResiliencePipeline CreatePipeline(UsagePulsePipelineOptions options, ILogger<UsageEventProcessor> logger)
    {
        var maxRetries = Math.Max(0, options.MaxProcessingAttempts - 1);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromMilliseconds(options.BaseRetryDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = arguments =>
                {
                    logger.LogWarning(arguments.Outcome.Exception, "Retrying usage event processing. Attempt={Attempt} Delay={Delay}", arguments.AttemptNumber + 1, arguments.RetryDelay);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSamplingSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds)
            })
            .Build();
    }

    private static Activity? StartActivity(UsageEvent usageEvent)
    {
        var activity = ProcessingTelemetry.ActivitySource.StartActivity("usagepulse.processing.execute", ActivityKind.Consumer);
        activity?.SetTag("usagepulse.event.id", usageEvent.EventId);
        activity?.SetTag("usagepulse.tenant.id", usageEvent.TenantId);
        activity?.SetTag("usagepulse.feature", usageEvent.Feature);
        return activity;
    }

    private static void RecordMetric(UsageEvent usageEvent, string status, DeadLetterReasonCode? reasonCode)
    {
        ProcessingTelemetry.ProcessedEvents.Add(1,
            new KeyValuePair<string, object?>("tenant", usageEvent.TenantId),
            new KeyValuePair<string, object?>("feature", usageEvent.Feature),
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("reasonCode", reasonCode?.ToString()));
    }
}
