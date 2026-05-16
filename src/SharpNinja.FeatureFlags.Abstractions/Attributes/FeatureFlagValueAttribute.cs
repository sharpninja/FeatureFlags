namespace SharpNinja.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-12 Phase 0 contract: declares a compile-time flag value metadata entry.</summary>
/// <remarks>
/// Source generator marker; declares the strongly-typed return value for the generated accessor.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-12"/>
/// </remarks>
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
