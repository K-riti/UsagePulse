using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsagePulse.Contracts;

[JsonConverter(typeof(EventIdJsonConverter))]
public readonly record struct EventId
{
    public string Value { get; }

    [JsonConstructor]
    public EventId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("EventId is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;

    public static implicit operator string(EventId value) => value.Value;

    public static implicit operator EventId(string value) => new(value);
}

[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId
{
    public string Value { get; }

    [JsonConstructor]
    public TenantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("TenantId is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;

    public static implicit operator string(TenantId value) => value.Value;

    public static implicit operator TenantId(string value) => new(value);
}

[JsonConverter(typeof(FeatureNameJsonConverter))]
public readonly record struct FeatureName
{
    public string Value { get; }

    [JsonConstructor]
    public FeatureName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Feature is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;

    public static implicit operator string(FeatureName value) => value.Value;

    public static implicit operator FeatureName(string value) => new(value);
}

internal sealed class EventIdJsonConverter : JsonConverter<EventId>
{
    public override EventId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, EventId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

internal sealed class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}

internal sealed class FeatureNameJsonConverter : JsonConverter<FeatureName>
{
    public override FeatureName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, FeatureName value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
