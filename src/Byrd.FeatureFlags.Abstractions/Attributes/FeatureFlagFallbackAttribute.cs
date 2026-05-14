namespace Byrd.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-12 Phase 0 contract: declares a generated accessor fallback member.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FeatureFlagFallbackAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="FeatureFlagFallbackAttribute"/> class.</summary>
    /// <param name="memberName">Fallback member name.</param>
    public FeatureFlagFallbackAttribute(string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        MemberName = memberName;
    }

    /// <summary>Gets the fallback member name.</summary>
    public string MemberName { get; }
}
