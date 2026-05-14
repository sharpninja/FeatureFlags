using System.Collections.ObjectModel;

namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>TR-11 Phase 0 contract: builder for immutable evaluation contexts.</summary>
public sealed class EvaluationContextBuilder
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    private EvaluationContextBuilder()
    {
    }

    /// <summary>Creates a new builder instance.</summary>
    /// <returns>A context builder.</returns>
    public static EvaluationContextBuilder Create() => new();

    /// <summary>Adds or replaces a context value.</summary>
    /// <param name="name">Context value name.</param>
    /// <param name="value">Context value.</param>
    /// <returns>The current builder.</returns>
    public EvaluationContextBuilder Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = value;
        return this;
    }

    /// <summary>Builds the immutable context.</summary>
    /// <returns>An immutable evaluation context.</returns>
    public EvaluationContext Build()
        => new(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(_values)));
}
