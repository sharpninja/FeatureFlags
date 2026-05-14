namespace Byrd.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-7 FR-12 Phase 0 contract: declares a generated gate around a feature method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FeatureFlagGateAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="FeatureFlagGateAttribute"/> class.</summary>
    /// <param name="key">Feature flag key.</param>
    public FeatureFlagGateAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    /// <summary>Gets the feature flag key.</summary>
    public string Key { get; }

    /// <summary>Gets or sets disabled behavior.</summary>
    public DisabledBehavior DisabledBehavior { get; set; } = DisabledBehavior.ReturnFallback;
}
