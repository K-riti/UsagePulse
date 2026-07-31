using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.Infrastructure;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Tests;

public class UsageIngressPolicyEvaluatorTests
{
    [Fact]
    public void Evaluate_RejectsUnsupportedSchemaVersion()
    {
        var evaluator = CreateEvaluator();
        var usageEvent = TestEvent() with { SchemaVersion = 3 };

        var result = evaluator.Evaluate(usageEvent);

        Assert.False(result.IsAccepted);
        Assert.Equal(DeadLetterReasonCode.SchemaIncompatible, result.Failure?.ReasonCode);
    }

    [Fact]
    public void Evaluate_AllowsBurstButRejectsBeyondBurstLimit()
    {
        var evaluator = CreateEvaluator(requestsPerMinute: 10, burstMultiplier: 2);

        var burstResult = evaluator.Evaluate(TestEvent(quantity: 15));
        var rejectedResult = evaluator.Evaluate(TestEvent(quantity: 6));

        Assert.True(burstResult.IsAccepted);
        Assert.Equal("burst", burstResult.Mode);
        Assert.False(rejectedResult.IsAccepted);
        Assert.Equal(DeadLetterReasonCode.QuotaExceeded, rejectedResult.Failure?.ReasonCode);
    }

    [Fact]
    public void DeadLetterEnvelopeSerializer_RoundTripsEnvelope()
    {
        var usageEvent = TestEvent();
        var failure = new ProcessingFailure(DeadLetterReasonCode.QuotaExceeded, "quota exceeded", ValidationErrorCode.QuotaExceeded);

        var payload = DeadLetterEnvelopeSerializer.Serialize(usageEvent, failure);
        var envelope = DeadLetterEnvelopeSerializer.Deserialize(payload);

        Assert.NotNull(envelope);
        Assert.Equal(usageEvent, envelope!.UsageEvent);
        Assert.Equal(failure, envelope.Failure);
    }

    private static UsageIngressPolicyEvaluator CreateEvaluator(int requestsPerMinute = 5000, double burstMultiplier = 2)
    {
        return new UsageIngressPolicyEvaluator(new UsagePulseSettings
        {
            CurrentSchemaVersion = 2,
            MinimumCompatibleSchemaVersion = 1,
            DefaultTenantQuota = new TenantQuotaPolicySettings
            {
                RequestsPerMinute = requestsPerMinute,
                BurstMultiplier = burstMultiplier
            }
        });
    }

    private static UsageEvent TestEvent(int quantity = 1) => new(
        Guid.NewGuid().ToString("N"),
        "tenant-a",
        "user-a",
        "dashboard",
        quantity,
        DateTimeOffset.UtcNow,
        SchemaVersion: 2,
        Source: "tests");
}
