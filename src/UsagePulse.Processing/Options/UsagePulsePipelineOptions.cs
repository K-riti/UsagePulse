namespace UsagePulse.Processing.Options;

public sealed class UsagePulsePipelineOptions
{
    public int MaxProcessingAttempts { get; set; } = 3;

    public int BaseRetryDelayMs { get; set; } = 250;
}
