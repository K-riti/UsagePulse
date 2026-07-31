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

    public string KustoIngestionEndpoint { get; set; } = string.Empty;

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
