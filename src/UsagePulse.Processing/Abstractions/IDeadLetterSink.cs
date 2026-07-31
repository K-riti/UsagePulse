using UsagePulse.Contracts;

namespace UsagePulse.Processing.Abstractions;

public interface IDeadLetterSink
{
    Task PublishAsync(UsageEvent usageEvent, ProcessingFailure failure, CancellationToken cancellationToken);
}
