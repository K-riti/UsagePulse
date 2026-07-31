namespace UsagePulse.Contracts;

public sealed record TenantUsageSummary(
    string TenantId,
    DateTimeOffset From,
    DateTimeOffset To,
    int EventCount,
    long TotalQuantity,
    IReadOnlyDictionary<string, int> FeatureBreakdown);
