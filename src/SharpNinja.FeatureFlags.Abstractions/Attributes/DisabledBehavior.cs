namespace SharpNinja.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-7 Phase 0 contract: behavior used when a gated feature is disabled.</summary>
/// <remarks>
/// Members carry stable ordinal values that consumers may persist; treat the enumeration as part of the public contract.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// </remarks>
public enum DisabledBehavior
{
    /// <summary>Return the declared fallback value.</summary>
    ReturnFallback = 0,

    /// <summary>Skip the gated operation.</summary>
    Skip = 1,

    /// <summary>Throw a feature-disabled exception in later implementation phases.</summary>
    Throw = 2,
}
