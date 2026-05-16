namespace SharpNinja.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-12 Phase 0 contract: declares the feature flag key for generated accessors.</summary>
/// <remarks>
/// Source generator marker; emitted into property and method targets. The constructor validates the key but the attribute itself is stateless once constructed.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-12"/>
/// </remarks>
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
