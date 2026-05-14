using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags;

/// <summary>FR-4 TR-11 Phase 1 SDK client that evaluates feature flags from the active manifest.</summary>
public sealed class SharpNinjaFeatureClient : ISharpNinjaFeatureClient
{
    private readonly FeatureFlagEvaluator evaluator;
    private readonly FeatureFlagManifest manifest;
    private readonly SharpNinjaFeatureFlagOptions options;

    /// <summary>Creates a new SDK client from DI-managed collaborators.</summary>
    /// <param name="evaluator">Feature flag evaluator.</param>
    /// <param name="manifest">Parsed feature flag manifest.</param>
    /// <param name="options">SDK feature flag options.</param>
    public SharpNinjaFeatureClient(
        FeatureFlagEvaluator evaluator,
        FeatureFlagManifest manifest,
        SharpNinjaFeatureFlagOptions options)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);

        this.evaluator = evaluator;
        this.manifest = manifest;
        this.options = options;
    }

    /// <inheritdoc />
    public EvaluationResult<T> Evaluate<T>(string key, T defaultValue, EvaluationContext? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return evaluator.Evaluate(manifest, options.ProductId, key, defaultValue, context);
    }

    /// <inheritdoc />
    public ValueTask<EvaluationResult<T>> EvaluateAsync<T>(
        string key,
        T defaultValue,
        EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<EvaluationResult<T>>(Task.FromCanceled<EvaluationResult<T>>(cancellationToken));
        }

        return ValueTask.FromResult(Evaluate(key, defaultValue, context));
    }
}
