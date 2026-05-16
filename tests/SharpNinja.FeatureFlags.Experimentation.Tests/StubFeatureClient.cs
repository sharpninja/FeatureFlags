using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Experimentation.Tests;

/// <summary>
/// Stub implementation of <see cref="ISharpNinjaFeatureClient"/> for unit tests.
/// Returns a fixed boolean value for every flag evaluation.
/// </summary>
internal sealed class StubFeatureClient : ISharpNinjaFeatureClient
{
    private readonly bool _enabled;

    /// <summary>Initializes the stub with a fixed enabled/disabled state.</summary>
    /// <param name="enabled">Value returned for every boolean flag evaluation.</param>
    public StubFeatureClient(bool enabled)
    {
        _enabled = enabled;
    }

    /// <inheritdoc/>
    public EvaluationResult<T> Evaluate<T>(string key, T defaultValue, EvaluationContext? context = null)
    {
        if (typeof(T) == typeof(bool))
        {
            T value = (T)(object)_enabled;
            return new EvaluationResult<T>(value, EvaluationReason.Default);
        }

        return new EvaluationResult<T>(defaultValue, EvaluationReason.Default);
    }

    /// <inheritdoc/>
    public ValueTask<EvaluationResult<T>> EvaluateAsync<T>(
        string key,
        T defaultValue,
        EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Evaluate<T>(key, defaultValue, context));
    }
}
