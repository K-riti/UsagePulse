using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.Infrastructure;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Functions;

public sealed class UsageIngestionFunction : IAsyncDisposable
{
    private readonly ServiceBusSender sender;
    private readonly IDeadLetterSink deadLetterSink;
    private readonly UsageIngressPolicyEvaluator ingressPolicyEvaluator;
    private readonly ILogger<UsageIngestionFunction> logger;

    public UsageIngestionFunction(
        ServiceBusClient serviceBusClient,
        UsagePulseSettings settings,
        IDeadLetterSink deadLetterSink,
        UsageIngressPolicyEvaluator ingressPolicyEvaluator,
        ILogger<UsageIngestionFunction> logger)
    {
        sender = serviceBusClient.CreateSender(settings.ServiceBusQueue);
        this.deadLetterSink = deadLetterSink;
        this.ingressPolicyEvaluator = ingressPolicyEvaluator;
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

            var policyDecision = ingressPolicyEvaluator.Evaluate(usageEvent);
            if (!policyDecision.IsAccepted)
            {
                logger.LogWarning("Rejected event {EventId} during ingress. ReasonCode={ReasonCode} Message={Message}", usageEvent.EventId, policyDecision.Failure?.ReasonCode, policyDecision.Failure?.Message);
                if (policyDecision.Failure is not null)
                {
                    await deadLetterSink.PublishAsync(usageEvent, policyDecision.Failure, cancellationToken);
                }

                continue;
            }

            var message = new ServiceBusMessage(UsageEventJsonSerializer.Serialize(usageEvent))
            {
                MessageId = usageEvent.EventId,
                Subject = "usage-event",
                SessionId = usageEvent.TenantId
            };

            message.ApplicationProperties["schemaVersion"] = usageEvent.SchemaVersion;
            message.ApplicationProperties["quotaMode"] = policyDecision.Mode;
            message.ApplicationProperties["source"] = usageEvent.Source ?? "unknown";

            await sender.SendMessageAsync(message, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await sender.DisposeAsync();
    }
}
