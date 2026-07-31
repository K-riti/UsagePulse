namespace UsagePulse.Functions.Configuration;

public sealed class UsagePulseSettings
{
    public string EventHubName { get; set; } = "usage-events";

    public string ServiceBusQueue { get; set; } = "usage-events-work";

    public string DeadLetterQueue { get; set; } = "usage-events-dlq";

    public string ServiceBusNamespace { get; set; } = string.Empty;

    public string ServiceBusConnectionString { get; set; } = string.Empty;

    public string CosmosEndpoint { get; set; } = string.Empty;

    public string CosmosConnectionString { get; set; } = string.Empty;

    public string CosmosDatabase { get; set; } = "usagepulse";

    public string EventsContainer { get; set; } = "usage-events";

    public string IdempotencyContainer { get; set; } = "usage-idempotency";

    public string KustoIngestionEndpoint { get; set; } = string.Empty;

    public int MaxProcessingAttempts { get; set; } = 3;

    public int BaseRetryDelayMs { get; set; } = 250;
}
