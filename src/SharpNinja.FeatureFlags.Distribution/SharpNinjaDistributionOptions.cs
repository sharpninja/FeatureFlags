namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 options for the Distribution service runtime.</summary>
internal sealed class SharpNinjaDistributionOptions
{
    public SharpNinjaDistributionOptions(
        string defaultEnvironment,
        IReadOnlyDictionary<string, List<string>> productApiKeys)
    {
        ArgumentNullException.ThrowIfNull(productApiKeys);

        DefaultEnvironment = string.IsNullOrWhiteSpace(defaultEnvironment)
            ? "Development"
            : defaultEnvironment;
        ProductApiKeys = productApiKeys.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    public string DefaultEnvironment { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ProductApiKeys { get; }
}
