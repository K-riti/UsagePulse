using System.Text.Json;
using Azure.Messaging.ServiceBus;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Functions.Infrastructure;

public sealed class ServiceBusDeadLetterSink : IDeadLetterSink, IAsyncDisposable
{
    private readonly ServiceBusSender sender;

    public ServiceBusDeadLetterSink(ServiceBusClient serviceBusClient, UsagePulseSettings settings)
    {
        sender = serviceBusClient.CreateSender(settings.DeadLetterQueue);
    }

    public async Task PublishAsync(UsageEvent usageEvent, string reason, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            usageEvent,
            reason,
            failedAt = DateTimeOffset.UtcNow
        });

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
