namespace Byrd.FeatureFlags.Abstractions.Attributes;

/// <summary>FR-7 Phase 0 contract: behavior used when a gated feature is disabled.</summary>
public enum DisabledBehavior
{
    /// <summary>Return the declared fallback value.</summary>
    ReturnFallback = 0,

    /// <summary>Skip the gated operation.</summary>
    Skip = 1,

    /// <summary>Throw a feature-disabled exception in later implementation phases.</summary>
    Throw = 2,
}
