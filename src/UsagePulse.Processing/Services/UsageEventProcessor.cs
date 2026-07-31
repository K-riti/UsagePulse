using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Options;

namespace UsagePulse.Processing.Services;

public sealed class UsageEventProcessor : IUsageEventProcessor
{
    private readonly IIdempotencyStore idempotencyStore;
    private readonly IUsageEventRepository usageEventRepository;
    private readonly IUsageAnalyticsSink analyticsSink;
    private readonly IDeadLetterSink deadLetterSink;
    private readonly UsagePulsePipelineOptions options;
    private readonly ILogger<UsageEventProcessor> logger;

    public UsageEventProcessor(
        IIdempotencyStore idempotencyStore,
        IUsageEventRepository usageEventRepository,
        IUsageAnalyticsSink analyticsSink,
        IDeadLetterSink deadLetterSink,
        Microsoft.Extensions.Options.IOptions<UsagePulsePipelineOptions> options,
        ILogger<UsageEventProcessor> logger)
    {
        this.idempotencyStore = idempotencyStore;
        this.usageEventRepository = usageEventRepository;
        this.analyticsSink = analyticsSink;
        this.deadLetterSink = deadLetterSink;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<ProcessingResult> ProcessAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        if (!await idempotencyStore.TryStartProcessingAsync(usageEvent.EventId, cancellationToken))
        {
            logger.LogInformation("Usage event {EventId} is already processed.", usageEvent.EventId);
            return ProcessingResult.Duplicate();
        }

        var maxAttempts = Math.Max(1, options.MaxProcessingAttempts);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await usageEventRepository.StoreAsync(usageEvent, cancellationToken);
                await analyticsSink.WriteAsync(usageEvent, cancellationToken);
                await idempotencyStore.MarkProcessedAsync(usageEvent.EventId, cancellationToken);
                return ProcessingResult.Success(attempt);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * options.BaseRetryDelayMs);
                logger.LogWarning(ex, "Failed processing event {EventId} attempt {Attempt}. Retrying in {Delay}.", usageEvent.EventId, attempt, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing event {EventId} after {Attempts} attempts.", usageEvent.EventId, attempt);
                await deadLetterSink.PublishAsync(usageEvent, ex.Message, cancellationToken);
                return ProcessingResult.Failure(attempt, ex.Message);
            }
        }

        return ProcessingResult.Failure(maxAttempts, "Processing failed.");
    }
}
