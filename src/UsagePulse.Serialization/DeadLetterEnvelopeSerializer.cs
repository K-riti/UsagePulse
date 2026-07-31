using System.Text.Json;
using UsagePulse.Contracts;

namespace UsagePulse.Serialization;

public static class DeadLetterEnvelopeSerializer
{
    public static string Serialize(UsageEvent usageEvent, ProcessingFailure failure)
    {
        return JsonSerializer.Serialize(new DeadLetterEnvelope(usageEvent, failure, DateTimeOffset.UtcNow), UsagePulseJsonDefaults.Options);
    }

    private sealed record DeadLetterEnvelope(UsageEvent UsageEvent, ProcessingFailure Failure, DateTimeOffset FailedAt);
}
