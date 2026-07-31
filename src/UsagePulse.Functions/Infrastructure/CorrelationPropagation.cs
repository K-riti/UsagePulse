using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace UsagePulse.Functions.Infrastructure;

public static class CorrelationPropagation
{
    private const string TraceParentKey = "traceparent";
    private const string TraceStateKey = "tracestate";
    private const string CorrelationIdKey = "correlationId";

    public static void ApplyIncoming(ServiceBusReceivedMessage message, Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        if (message.ApplicationProperties.TryGetValue(CorrelationIdKey, out var correlationId) && correlationId is not null)
        {
            activity.SetTag("usagepulse.correlation_id", correlationId.ToString());
        }

        if (message.ApplicationProperties.TryGetValue(TraceParentKey, out var traceParent) && traceParent is not null)
        {
            activity.SetTag("usagepulse.trace_parent", traceParent.ToString());
        }

        if (message.ApplicationProperties.TryGetValue(TraceStateKey, out var traceState) && traceState is not null)
        {
            activity.SetTag("usagepulse.trace_state", traceState.ToString());
        }
    }

    public static void EnrichOutgoing(ServiceBusMessage message, Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(activity.Id))
        {
            message.ApplicationProperties[TraceParentKey] = activity.Id;
        }

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            message.ApplicationProperties[TraceStateKey] = activity.TraceStateString;
        }

        message.ApplicationProperties[CorrelationIdKey] = activity.TraceId.ToString();
    }
}
