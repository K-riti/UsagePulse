namespace UsagePulse.Functions.Configuration;

public sealed class SchemaContractRuleSettings
{
    public string Source { get; set; } = "*";

    public string Feature { get; set; } = "*";

    public int MinimumSchemaVersion { get; set; } = 1;

    public int MaximumSchemaVersion { get; set; } = 1;

    public bool Matches(string? source, string feature)
    {
        var sourceMatch = Source == "*" || string.Equals(Source, source, StringComparison.OrdinalIgnoreCase);
        var featureMatch = Feature == "*" || string.Equals(Feature, feature, StringComparison.OrdinalIgnoreCase);
        return sourceMatch && featureMatch;
    }
}
