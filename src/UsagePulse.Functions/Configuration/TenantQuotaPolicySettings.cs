using System.ComponentModel.DataAnnotations;

namespace UsagePulse.Functions.Configuration;

public sealed class TenantQuotaPolicySettings
{
    [Range(10, 1_000_000)]
    public int RequestsPerMinute { get; set; } = 5000;

    [Range(1.0, 10.0)]
    public double BurstMultiplier { get; set; } = 2.0;
}

public sealed class TenantQuotaOverrideSettings
{
    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Range(10, 1_000_000)]
    public int RequestsPerMinute { get; set; } = 5000;

    [Range(1.0, 10.0)]
    public double BurstMultiplier { get; set; } = 2.0;
}
