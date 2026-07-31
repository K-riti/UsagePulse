namespace UsagePulse.Contracts;

public sealed record UsageEvent(
    string EventId,
    string TenantId,
    string UserId,
    string Feature,
    int Quantity,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string>? Dimensions = null,
    int SchemaVersion = 1,
    string? Source = null);
