using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Infrastructure;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Services;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Orchestration;

public sealed class UsageProcessingOrchestrator
{
    private readonly IUsageEventProcessor processor;
    private readonly ILogger<UsageProcessingOrchestrator> logger;

    public UsageProcessingOrchestrator(IUsageEventProcessor processor, ILogger<UsageProcessingOrchestrator> logger)
    {
        this.processor = processor;
        this.logger = logger;
    }

    public async Task HandleAsync(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, CancellationToken cancellationToken)
    {
        using var activity = ProcessingTelemetry.ActivitySource.StartActivity("usagepulse.functions.processing", ActivityKind.Consumer);
        CorrelationPropagation.ApplyIncoming(message, activity);

        UsageEvent? usageEvent;
        try
        {
            usageEvent = UsageEventJsonSerializer.Deserialize(message.Body.ToString());
        }
        catch (Exception ex)
        {
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: DeadLetterReasonCode.InvalidPayload.ToString(), deadLetterErrorDescription: ex.Message, cancellationToken: cancellationToken);
            return;
        }

        if (usageEvent is null)
        {
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: DeadLetterReasonCode.NullPayload.ToString(), deadLetterErrorDescription: "Payload deserialized to null.", cancellationToken: cancellationToken);
            return;
        }

        var result = await processor.ProcessAsync(usageEvent, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogError("Processing failed for event {EventId}. attempts={Attempts} dead_letter_reason={DeadLetterReason} failure_message={FailureMessage}", usageEvent.EventId, result.Attempts, result.Failure?.ReasonCode, result.Failure?.Message);
        }

        await messageActions.CompleteMessageAsync(message, cancellationToken);
    }
}
