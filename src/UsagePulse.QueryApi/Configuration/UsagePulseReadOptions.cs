namespace UsagePulse.QueryApi.Configuration;

public sealed class UsagePulseReadOptions
{
    public string CosmosEndpoint { get; set; } = string.Empty;

    public string CosmosConnectionString { get; set; } = string.Empty;

    public string CosmosDatabase { get; set; } = "usagepulse";

    public string EventsContainer { get; set; } = "usage-events";
}
