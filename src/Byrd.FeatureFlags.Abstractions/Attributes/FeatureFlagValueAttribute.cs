namespace Byrd.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-12 Phase 0 contract: declares a compile-time flag value metadata entry.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class FeatureFlagValueAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="FeatureFlagValueAttribute"/> class.</summary>
    /// <param name="name">Value name.</param>
    /// <param name="value">Value payload.</param>
    public FeatureFlagValueAttribute(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Value = value;
    }

    /// <summary>Gets the value name.</summary>
    public string Name { get; }

    /// <summary>Gets the value payload.</summary>
    public object? Value { get; }
}
