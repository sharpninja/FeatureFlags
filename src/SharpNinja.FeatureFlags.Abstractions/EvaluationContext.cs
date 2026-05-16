using System.Collections.ObjectModel;

namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>TR-11 Phase 0 contract: immutable feature flag evaluation context.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
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
