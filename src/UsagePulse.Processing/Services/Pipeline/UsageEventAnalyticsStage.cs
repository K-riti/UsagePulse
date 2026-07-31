using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed class UsageEventAnalyticsStage : IUsageEventAnalyticsStage
{
    private readonly IUsageAnalyticsSink analyticsSink;

    public UsageEventAnalyticsStage(IUsageAnalyticsSink analyticsSink)
    {
        this.analyticsSink = analyticsSink;
    }

    public Task ExportAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        return analyticsSink.WriteAsync(usageEvent, cancellationToken);
    }
}
