namespace Byrd.FeatureFlags.Abstractions;

/// <summary>TR-11 Phase 0 contract: immutable result returned by feature flag evaluation.</summary>
/// <typeparam name="T">Resolved value type.</typeparam>
public sealed record EvaluationResult<T>(
    T Value,
    EvaluationReason Reason,
    string? Variant = null,
    string? ErrorMessage = null);
