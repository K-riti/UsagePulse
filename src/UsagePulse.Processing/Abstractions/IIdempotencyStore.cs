namespace UsagePulse.Processing.Abstractions;

public interface IIdempotencyStore
{
    Task<bool> TryStartProcessingAsync(string eventId, CancellationToken cancellationToken);

    Task MarkProcessedAsync(string eventId, CancellationToken cancellationToken);
}
