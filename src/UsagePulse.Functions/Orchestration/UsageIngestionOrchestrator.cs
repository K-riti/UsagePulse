using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Infrastructure;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Orchestration;

public sealed class UsageIngestionOrchestrator
{
    private readonly ServiceBusSender sender;
    private readonly IDeadLetterSink deadLetterSink;
    private readonly UsageIngressPolicyEvaluator ingressPolicyEvaluator;
    private readonly ILogger<UsageIngestionOrchestrator> logger;

    public UsageIngestionOrchestrator(
        ServiceBusSender sender,
        IDeadLetterSink deadLetterSink,
        UsageIngressPolicyEvaluator ingressPolicyEvaluator,
        ILogger<UsageIngestionOrchestrator> logger)
    {
        this.sender = sender;
        this.deadLetterSink = deadLetterSink;
        this.ingressPolicyEvaluator = ingressPolicyEvaluator;
        this.logger = logger;
    }

    public async Task HandleBatchAsync(string[] events, CancellationToken cancellationToken)
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
                logger.LogWarning("Rejected event {EventId}. dead_letter_reason={DeadLetterReason} dead_letter_message={DeadLetterMessage}", usageEvent.EventId, policyDecision.Failure?.ReasonCode, policyDecision.Failure?.Message);
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
            CorrelationPropagation.EnrichOutgoing(message, Activity.Current);

            await sender.SendMessageAsync(message, cancellationToken);
        }
    }
}
