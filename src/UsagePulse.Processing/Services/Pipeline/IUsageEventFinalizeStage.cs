using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public interface IUsageEventFinalizeStage
{
    Task FinalizeAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
