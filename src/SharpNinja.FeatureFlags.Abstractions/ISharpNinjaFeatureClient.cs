namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-4 TR-11 Phase 0 contract: DI-resolved client for evaluating SharpNinja Feature Flags.</summary>
public interface ISharpNinjaFeatureClient
{
    /// <summary>Evaluates a flag value without blocking on network state.</summary>
    /// <typeparam name="T">Flag value type.</typeparam>
    /// <param name="key">Flag key.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="context">Optional evaluation context.</param>
    /// <returns>The resolved evaluation result.</returns>
    EvaluationResult<T> Evaluate<T>(string key, T defaultValue, EvaluationContext? context = null);

    /// <summary>Evaluates a flag value through an asynchronous-compatible API surface.</summary>
    /// <typeparam name="T">Flag value type.</typeparam>
    /// <param name="key">Flag key.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="context">Optional evaluation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved evaluation result.</returns>
    ValueTask<EvaluationResult<T>> EvaluateAsync<T>(
        string key,
        T defaultValue,
        EvaluationContext? context = null,
        CancellationToken cancellationToken = default);
}
