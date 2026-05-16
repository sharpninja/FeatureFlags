namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-11 Phase 1 contract: immutable result returned by feature flag evaluation.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
/// <typeparam name="T">Resolved value type.</typeparam>
/// <param name="Value">Resolved feature flag value.</param>
/// <param name="Reason">Reason the value was resolved.</param>
/// <param name="Variant">Optional variant identifier.</param>
/// <param name="ErrorMessage">Optional evaluation error detail.</param>
/// <param name="RuleIndex">Optional zero-based index of the matched rule.</param>
public sealed record EvaluationResult<T>(
    T Value,
    EvaluationReason Reason,
    string? Variant = null,
    string? ErrorMessage = null,
    int? RuleIndex = null);
