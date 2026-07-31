using UsagePulse.Contracts;

namespace UsagePulse.Processing.Abstractions;

public interface IUsageEventRepository
{
    Task StoreAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
