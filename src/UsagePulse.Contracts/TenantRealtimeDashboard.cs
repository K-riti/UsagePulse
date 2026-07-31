namespace UsagePulse.Contracts;

public sealed record TenantUsagePoint(
    DateTimeOffset BucketStart,
    DateTimeOffset BucketEnd,
    int EventCount,
    long TotalQuantity);

public sealed record TenantRealtimeDashboard(
    string TenantId,
    DateTimeOffset From,
    DateTimeOffset To,
    int EventCount,
    long TotalQuantity,
    IReadOnlyDictionary<string, int> FeatureBreakdown,
    IReadOnlyList<TenantUsagePoint> Points);
