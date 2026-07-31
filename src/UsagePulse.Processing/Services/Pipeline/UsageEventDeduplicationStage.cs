using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed class UsageEventDeduplicationStage : IUsageEventDeduplicationStage
{
    private readonly IIdempotencyStore idempotencyStore;

    public UsageEventDeduplicationStage(IIdempotencyStore idempotencyStore)
    {
        this.idempotencyStore = idempotencyStore;
    }

    public Task<bool> TryStartAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        return idempotencyStore.TryStartProcessingAsync(usageEvent.EventId, cancellationToken);
    }
}
