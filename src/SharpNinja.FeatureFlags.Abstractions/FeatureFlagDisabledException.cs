namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>
/// FR-7 FR-12: thrown by generated feature-flag gate accessors when the flag is disabled
/// and the gate declares <see cref="Attributes.DisabledBehavior.Throw"/>.
/// </summary>
/// <remarks>
/// Stateless after construction; safe to rethrow across threads. Never caught and swallowed inside the SDK.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-12"/>
/// </remarks>
public sealed class FeatureFlagDisabledException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="FeatureFlagDisabledException"/>.</summary>
    /// <param name="flagKey">The flag key that was evaluated as disabled.</param>
    public FeatureFlagDisabledException(string flagKey)
        : base($"Feature flag '{flagKey}' is disabled. The gated operation cannot proceed.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        FlagKey = flagKey;
    }

    /// <summary>Initializes a new instance of <see cref="FeatureFlagDisabledException"/>.</summary>
    /// <param name="flagKey">The flag key that was evaluated as disabled.</param>
    /// <param name="message">Custom exception message.</param>
    public FeatureFlagDisabledException(string flagKey, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        FlagKey = flagKey;
    }

    /// <summary>Initializes a new instance of <see cref="FeatureFlagDisabledException"/>.</summary>
    /// <param name="flagKey">The flag key that was evaluated as disabled.</param>
    /// <param name="message">Custom exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public FeatureFlagDisabledException(string flagKey, string message, Exception innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        FlagKey = flagKey;
    }

    /// <summary>Gets the feature flag key that was evaluated as disabled.</summary>
    public string FlagKey { get; }
}
