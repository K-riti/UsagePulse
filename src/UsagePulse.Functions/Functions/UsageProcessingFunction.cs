using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Functions;

public sealed class UsageProcessingFunction
{
    private readonly IUsageEventProcessor processor;
    private readonly ILogger<UsageProcessingFunction> logger;

    public UsageProcessingFunction(IUsageEventProcessor processor, ILogger<UsageProcessingFunction> logger)
    {
        this.processor = processor;
        this.logger = logger;
    }

    [Function(nameof(UsageProcessingFunction))]
    public async Task Run(
        [ServiceBusTrigger("%UsagePulse:ServiceBusQueue%", Connection = "UsagePulseServiceBusConnection")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
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
            logger.LogError("Processing failed for event {EventId}. Attempts={Attempts} ReasonCode={ReasonCode} Message={FailureMessage}", usageEvent.EventId, result.Attempts, result.Failure?.ReasonCode, result.Failure?.Message);
        }

        await messageActions.CompleteMessageAsync(message, cancellationToken);
    }
}
