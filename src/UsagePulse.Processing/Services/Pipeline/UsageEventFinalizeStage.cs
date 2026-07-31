using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed class UsageEventFinalizeStage : IUsageEventFinalizeStage
{
    private readonly IIdempotencyStore idempotencyStore;

    public UsageEventFinalizeStage(IIdempotencyStore idempotencyStore)
    {
        this.idempotencyStore = idempotencyStore;
    }

    public Task FinalizeAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        return idempotencyStore.MarkProcessedAsync(usageEvent.EventId, cancellationToken);
    }
}
