using System.Text.Json;
using UsagePulse.Contracts;

namespace UsagePulse.Serialization;

public static class UsageEventJsonSerializer
{
    public static UsageEvent? Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var usageEvent = JsonSerializer.Deserialize<UsageEvent>(payload, UsagePulseJsonDefaults.Options);
        if (usageEvent is null)
        {
            return null;
        }

        var failure = UsageEventContractValidator.Validate(usageEvent);
        if (failure is not null)
        {
            throw new JsonException(failure.Message);
        }

        return usageEvent;
    }

    public static string Serialize(UsageEvent usageEvent)
    {
        var failure = UsageEventContractValidator.Validate(usageEvent);
        if (failure is not null)
        {
            throw new JsonException(failure.Message);
        }

        return JsonSerializer.Serialize(usageEvent, UsagePulseJsonDefaults.Options);
    }
}
