using UsagePulse.Contracts;

namespace UsagePulse.Processing.Abstractions;

public interface IDeadLetterSink
{
    Task PublishAsync(UsageEvent usageEvent, string reason, CancellationToken cancellationToken);
}
