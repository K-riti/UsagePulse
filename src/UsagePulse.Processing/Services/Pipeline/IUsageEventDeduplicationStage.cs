using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public interface IUsageEventDeduplicationStage
{
    Task<bool> TryStartAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
