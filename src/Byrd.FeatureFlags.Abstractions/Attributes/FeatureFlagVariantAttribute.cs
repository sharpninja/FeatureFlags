namespace Byrd.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-12 Phase 0 contract: declares a variant key supported by a flag member.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class FeatureFlagVariantAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="FeatureFlagVariantAttribute"/> class.</summary>
    /// <param name="variant">Variant key.</param>
    public FeatureFlagVariantAttribute(string variant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variant);
        Variant = variant;
    }

    /// <summary>Gets the variant key.</summary>
    public string Variant { get; }
}
