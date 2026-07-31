using Azure.Messaging.ServiceBus;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Infrastructure;

public sealed class ServiceBusDeadLetterSink : IDeadLetterSink, IAsyncDisposable
{
    private readonly ServiceBusSender sender;

    public ServiceBusDeadLetterSink(ServiceBusClient serviceBusClient, UsagePulseSettings settings)
    {
        sender = serviceBusClient.CreateSender(settings.DeadLetterQueue);
    }

    public async Task PublishAsync(UsageEvent usageEvent, ProcessingFailure failure, CancellationToken cancellationToken)
    {
        var body = DeadLetterEnvelopeSerializer.Serialize(usageEvent, failure);

        var message = new ServiceBusMessage(body)
        {
            MessageId = usageEvent.EventId,
            Subject = "usage-event-dead-letter"
        };

        await sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await sender.DisposeAsync();
    }
}
