using System.ComponentModel.DataAnnotations;

namespace UsagePulse.Functions.Configuration;

public sealed class UsagePulseSettings
{
    [Required]
    public string EventHubName { get; set; } = "usage-events";

    [Required]
    public string ServiceBusQueue { get; set; } = "usage-events-work";

    [Required]
    public string DeadLetterQueue { get; set; } = "usage-events-dlq";

    public string ServiceBusNamespace { get; set; } = string.Empty;

    public string ServiceBusConnectionString { get; set; } = string.Empty;

    public string CosmosEndpoint { get; set; } = string.Empty;

    public string CosmosConnectionString { get; set; } = string.Empty;

    [Required]
    public string CosmosDatabase { get; set; } = "usagepulse";

    [Required]
    public string EventsContainer { get; set; } = "usage-events";

    [Required]
    public string IdempotencyContainer { get; set; } = "usage-idempotency";

    [Required]
    public string SummaryViewsContainer { get; set; } = "usage-summary-views";

    public string KustoClusterUri { get; set; } = string.Empty;

    public string KustoDatabase { get; set; } = "usagepulse";

    public string KustoTable { get; set; } = "UsageEvents";

    public string KustoManagedIdentityClientId { get; set; } = string.Empty;

    [Range(1, 5000)]
    public int KustoBatchSize { get; set; } = 250;

    [Range(1, 60)]
    public int KustoFlushIntervalSeconds { get; set; } = 5;

    [Range(1, 100)]
    public int CurrentSchemaVersion { get; set; } = 1;

    [Range(1, 100)]
    public int MinimumCompatibleSchemaVersion { get; set; } = 1;

    public string KeyVaultUri { get; set; } = string.Empty;

    public bool AllowConnectionStringFallback { get; set; }

    public List<SchemaContractRuleSettings> SchemaContracts { get; set; } = [];

    [Required]
    public TenantQuotaPolicySettings DefaultTenantQuota { get; set; } = new();

    public List<TenantQuotaOverrideSettings> TenantQuotas { get; set; } = [];

    [Range(1, 10)]
    public int MaxProcessingAttempts { get; set; } = 3;

    [Range(50, 10000)]
    public int BaseRetryDelayMs { get; set; } = 250;

    [Range(5, 300)]
    public int CircuitBreakerSamplingSeconds { get; set; } = 30;

    [Range(5, 300)]
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    [Range(2, 100)]
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    [Range(0.1, 1.0)]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
}
