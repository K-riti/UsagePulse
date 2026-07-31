namespace UsagePulse.Contracts;

public sealed record UsageEvent(
    EventId EventId,
    TenantId TenantId,
    string UserId,
    FeatureName Feature,
    int Quantity,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string>? Dimensions = null,
    int SchemaVersion = 1,
    string? Source = null);
