namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 builder for Distribution service registration.</summary>
public sealed class SharpNinjaDistributionBuilder
{
    internal SharpNinjaDistributionBuilder()
    {
    }

    /// <summary>FR-6 v1 default manifest environment used when callers omit the environment query value.</summary>
    public string DefaultEnvironment { get; set; } = "Development";

    /// <summary>TR-9 v1 in-memory product API key map keyed by ProductId.</summary>
    public Dictionary<string, List<string>> ProductApiKeys { get; } = new(StringComparer.Ordinal);

    internal SharpNinjaDistributionOptions BuildOptions() =>
        new(DefaultEnvironment, ProductApiKeys);
}
