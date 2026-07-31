using Newtonsoft.Json;

namespace UsagePulse.QueryApi.StorageModels;

public sealed class UsageSummaryProjectionDocument
{
    [JsonProperty("feature")]
    public required string Feature { get; init; }

    [JsonProperty("quantity")]
    public required int Quantity { get; init; }
}
