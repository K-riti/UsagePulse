using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace UsagePulse.Processing.Services;

public static class ProcessingTelemetry
{
    public const string ActivitySourceName = "UsagePulse.Processing";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new("UsagePulse.Processing.Metrics", "1.0.0");

    public static readonly Counter<long> ProcessedEvents = Meter.CreateCounter<long>("usagepulse.events.processed");
}
