using Newtonsoft.Json;

namespace UsagePulse.QueryApi.StorageModels;

public sealed record UsageSummaryViewDocument
{
    [JsonProperty("id")]
    public required string Id { get; init; }

    [JsonProperty("tenantId")]
    public required string TenantId { get; init; }

    [JsonProperty("window")]
    public required string Window { get; init; }

    [JsonProperty("bucketStart")]
    public required DateTimeOffset BucketStart { get; init; }

    [JsonProperty("bucketEnd")]
    public required DateTimeOffset BucketEnd { get; init; }

    [JsonProperty("eventCount")]
    public int EventCount { get; init; }

    [JsonProperty("totalQuantity")]
    public long TotalQuantity { get; init; }

    [JsonProperty("featureBreakdown")]
    public Dictionary<string, int> FeatureBreakdown { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}
