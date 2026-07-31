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

        return JsonSerializer.Deserialize<UsageEvent>(payload, UsagePulseJsonDefaults.Options);
    }

    public static string Serialize(UsageEvent usageEvent)
    {
        return JsonSerializer.Serialize(usageEvent, UsagePulseJsonDefaults.Options);
    }
}
