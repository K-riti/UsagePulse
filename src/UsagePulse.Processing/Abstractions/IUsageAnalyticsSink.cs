using UsagePulse.Contracts;

namespace UsagePulse.Processing.Abstractions;

public interface IUsageAnalyticsSink
{
    Task WriteAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
