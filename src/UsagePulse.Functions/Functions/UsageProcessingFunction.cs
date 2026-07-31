using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;

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
            usageEvent = JsonSerializer.Deserialize<UsageEvent>(message.Body.ToString());
        }
        catch (JsonException ex)
        {
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "InvalidPayload", deadLetterErrorDescription: ex.Message, cancellationToken: cancellationToken);
            return;
        }

        if (usageEvent is null)
        {
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "NullPayload", deadLetterErrorDescription: "Payload deserialized to null.", cancellationToken: cancellationToken);
            return;
        }

        var result = await processor.ProcessAsync(usageEvent, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogError("Event {EventId} failed after {Attempts} attempts. Message moved to application dead-letter queue.", usageEvent.EventId, result.Attempts);
        }

        await messageActions.CompleteMessageAsync(message, cancellationToken);
    }
}
