using System.Collections.ObjectModel;

namespace Byrd.FeatureFlags.Abstractions;

/// <summary>TR-11 Phase 0 contract: immutable feature flag evaluation context.</summary>
/// <param name="Values">Context values supplied to the evaluator.</param>
public sealed record EvaluationContext(IReadOnlyDictionary<string, object?> Values)
{
    /// <summary>Gets an empty evaluation context.</summary>
    public static EvaluationContext Empty { get; } =
        new(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()));

    /// <summary>Creates a builder for a new evaluation context.</summary>
    /// <returns>A context builder.</returns>
    public static EvaluationContextBuilder Builder() => EvaluationContextBuilder.Create();
}
