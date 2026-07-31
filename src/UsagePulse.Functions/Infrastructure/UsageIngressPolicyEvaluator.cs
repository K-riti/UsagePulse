using System.Collections.Concurrent;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;

namespace UsagePulse.Functions.Infrastructure;

public sealed class UsageIngressPolicyEvaluator
{
    private readonly UsagePulseSettings settings;
    private readonly ConcurrentDictionary<string, TenantQuotaWindow> tenantWindows = new(StringComparer.OrdinalIgnoreCase);

    public UsageIngressPolicyEvaluator(UsagePulseSettings settings)
    {
        this.settings = settings;
    }

    public UsageIngressPolicyDecision Evaluate(UsageEvent usageEvent)
    {
        if (usageEvent.SchemaVersion < settings.MinimumCompatibleSchemaVersion || usageEvent.SchemaVersion > settings.CurrentSchemaVersion)
        {
            return UsageIngressPolicyDecision.Reject(new ProcessingFailure(
                DeadLetterReasonCode.SchemaIncompatible,
                $"Schema version {usageEvent.SchemaVersion} is not compatible with supported range {settings.MinimumCompatibleSchemaVersion}-{settings.CurrentSchemaVersion}.",
                ValidationErrorCode.UnsupportedSchemaVersion));
        }

        var policy = ResolvePolicy(usageEvent.TenantId);
        var window = tenantWindows.GetOrAdd(usageEvent.TenantId, _ => new TenantQuotaWindow());
        var units = Math.Max(1, usageEvent.Quantity);
        var decision = window.TryConsume(units, policy.RequestsPerMinute, policy.BurstMultiplier);
        if (!decision.IsAccepted)
        {
            return UsageIngressPolicyDecision.Reject(new ProcessingFailure(
                DeadLetterReasonCode.QuotaExceeded,
                $"Tenant '{usageEvent.TenantId}' exceeded {policy.RequestsPerMinute}/minute quota with burst multiplier {policy.BurstMultiplier:0.##}.",
                ValidationErrorCode.QuotaExceeded));
        }

        return UsageIngressPolicyDecision.Accept(decision.IsBurst ? "burst" : "standard");
    }

    private TenantQuotaPolicySettings ResolvePolicy(string tenantId)
    {
        var overrideSettings = settings.TenantQuotas.FirstOrDefault(x => string.Equals(x.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
        if (overrideSettings is null)
        {
            return settings.DefaultTenantQuota;
        }

        return new TenantQuotaPolicySettings
        {
            RequestsPerMinute = overrideSettings.RequestsPerMinute,
            BurstMultiplier = overrideSettings.BurstMultiplier
        };
    }

    private sealed class TenantQuotaWindow
    {
        private readonly object gate = new();
        private DateTimeOffset windowStart = FloorToMinute(DateTimeOffset.UtcNow);
        private int usedUnits;

        public TenantQuotaWindowDecision TryConsume(int units, int requestsPerMinute, double burstMultiplier)
        {
            lock (gate)
            {
                var now = DateTimeOffset.UtcNow;
                var currentWindow = FloorToMinute(now);
                if (currentWindow != windowStart)
                {
                    windowStart = currentWindow;
                    usedUnits = 0;
                }

                var normalLimit = Math.Max(1, requestsPerMinute);
                var burstLimit = Math.Max(normalLimit, (int)Math.Ceiling(normalLimit * burstMultiplier));
                if (usedUnits + units > burstLimit)
                {
                    return TenantQuotaWindowDecision.Reject();
                }

                usedUnits += units;
                return TenantQuotaWindowDecision.Accept(usedUnits > normalLimit);
            }
        }

        private static DateTimeOffset FloorToMinute(DateTimeOffset value)
            => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Offset);
    }
}

public sealed record UsageIngressPolicyDecision(bool IsAccepted, string Mode, ProcessingFailure? Failure)
{
    public static UsageIngressPolicyDecision Accept(string mode) => new(true, mode, null);

    public static UsageIngressPolicyDecision Reject(ProcessingFailure failure) => new(false, "rejected", failure);
}

public sealed record TenantQuotaWindowDecision(bool IsAccepted, bool IsBurst)
{
    public static TenantQuotaWindowDecision Accept(bool isBurst) => new(true, isBurst);

    public static TenantQuotaWindowDecision Reject() => new(false, false);
}
