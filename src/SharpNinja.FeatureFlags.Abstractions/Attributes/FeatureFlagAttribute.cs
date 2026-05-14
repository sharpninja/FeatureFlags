namespace SharpNinja.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-12 Phase 0 contract: declares the feature flag key for generated accessors.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FeatureFlagAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="FeatureFlagAttribute"/> class.</summary>
    /// <param name="key">Feature flag key.</param>
    public FeatureFlagAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Gets the feature flag key.</summary>
    public string Key { get; }
}
