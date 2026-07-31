using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public interface IUsageEventAnalyticsStage
{
    Task ExportAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
