namespace Byrd.FeatureFlags.Abstractions;

/// <summary>TR-11 Phase 0 contract: describes why a feature flag evaluation resolved to its value.</summary>
public enum EvaluationReason
{
    /// <summary>The evaluator did not provide a specific reason.</summary>
    Unknown = 0,

    /// <summary>The default value was returned.</summary>
    Default = 1,

    /// <summary>A manifest rule matched.</summary>
    RuleMatch = 2,

    /// <summary>A targeting rule matched.</summary>
    TargetingMatch = 3,

    /// <summary>An error forced fallback behavior.</summary>
    Error = 4,

    /// <summary>The flag was disabled.</summary>
    Disabled = 5,
}
