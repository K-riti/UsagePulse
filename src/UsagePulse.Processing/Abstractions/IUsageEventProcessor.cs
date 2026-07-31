using UsagePulse.Contracts;

namespace UsagePulse.Processing.Abstractions;

public interface IUsageEventProcessor
{
    Task<ProcessingResult> ProcessAsync(UsageEvent usageEvent, CancellationToken cancellationToken);
}
