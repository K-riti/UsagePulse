using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Functions;

public sealed class UsageIngestionFunction : IAsyncDisposable
{
    private readonly ServiceBusSender sender;
    private readonly ILogger<UsageIngestionFunction> logger;

    public UsageIngestionFunction(
        ServiceBusClient serviceBusClient,
        UsagePulseSettings settings,
        ILogger<UsageIngestionFunction> logger)
    {
        sender = serviceBusClient.CreateSender(settings.ServiceBusQueue);
        this.logger = logger;
    }

    [Function(nameof(UsageIngestionFunction))]
    public async Task Run(
        [EventHubTrigger("%UsagePulse:EventHubName%", Connection = "UsagePulseEventHubConnection")] string[] events,
        CancellationToken cancellationToken)
    {
        foreach (var raw in events)
        {
            UsageEvent? usageEvent;
            try
            {
                usageEvent = UsageEventJsonSerializer.Deserialize(raw);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping invalid event payload in ingestion stage.");
                continue;
            }

            if (usageEvent is null)
            {
                continue;
            }

            var message = new ServiceBusMessage(UsageEventJsonSerializer.Serialize(usageEvent))
            {
                MessageId = usageEvent.EventId,
                Subject = "usage-event",
                SessionId = usageEvent.TenantId
            };

            await sender.SendMessageAsync(message, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await sender.DisposeAsync();
    }
}
