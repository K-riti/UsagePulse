using Newtonsoft.Json;

namespace UsagePulse.Functions.StorageModels;

public sealed class IdempotencyRecord
{
    [JsonProperty("id")]
    public required string Id { get; init; }

    [JsonProperty("status")]
    public required string Status { get; init; }

    [JsonProperty("processedAt")]
    public required DateTimeOffset ProcessedAt { get; init; }
}
