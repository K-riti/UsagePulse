using System.ComponentModel.DataAnnotations;

namespace UsagePulse.Processing.Options;

public sealed class UsagePulsePipelineOptions
{
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
