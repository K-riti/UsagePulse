using Newtonsoft.Json;

namespace UsagePulse.Functions.StorageModels;

public sealed class UsageEventDocument
{
    [JsonProperty("id")]
    public required string Id { get; init; }

    [JsonProperty("tenantId")]
    public required string TenantId { get; init; }

    [JsonProperty("userId")]
    public required string UserId { get; init; }

    [JsonProperty("feature")]
    public required string Feature { get; init; }

    [JsonProperty("quantity")]
    public required int Quantity { get; init; }

    [JsonProperty("occurredAt")]
    public required DateTimeOffset OccurredAt { get; init; }

    [JsonProperty("dimensions")]
    public IReadOnlyDictionary<string, string>? Dimensions { get; init; }

    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonProperty("source")]
    public string? Source { get; init; }

    [JsonProperty("ingestedAt")]
    public required DateTimeOffset IngestedAt { get; init; }
}
