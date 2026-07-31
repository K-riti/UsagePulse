using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed class UsageEventPersistenceStage : IUsageEventPersistenceStage
{
    private readonly IUsageEventRepository usageEventRepository;

    public UsageEventPersistenceStage(IUsageEventRepository usageEventRepository)
    {
        this.usageEventRepository = usageEventRepository;
    }

    public Task PersistAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        return usageEventRepository.StoreAsync(usageEvent, cancellationToken);
    }
}
