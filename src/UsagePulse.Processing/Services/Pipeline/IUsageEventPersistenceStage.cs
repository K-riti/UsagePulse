using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public interface IUsageEventPersistenceStage
{
    Task PersistAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
