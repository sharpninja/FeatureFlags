using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: a typed pipeline behavior marker interface for flag-aware behaviors.
/// Strongly-typed behaviors implement this interface to participate in the CQRS pipeline
/// alongside the non-generic <see cref="IPipelineBehavior"/>.
/// </summary>
/// <typeparam name="TRequest">The command or query type being dispatched.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public interface IPipelineBehavior<TRequest, TResult>
{
    /// <summary>
    /// Handles the pipeline step for a specific request type.
    /// Call <paramref name="nextStep"/> to continue to the next behavior or handler.
    /// </summary>
    /// <param name="request">The command or query being dispatched.</param>
    /// <param name="context">The call context for this dispatch.</param>
    /// <param name="nextStep">Delegate to invoke the next behavior or the handler.</param>
    /// <returns>The result from the handler or a short-circuit result.</returns>
    Task<Result<TResult>> HandleAsync(TRequest request, CallContext context, Func<Task<Result<TResult>>> nextStep);
}

/// <summary>
/// FR-7: a typed pipeline behavior that evaluates a feature flag before dispatch and throws
/// <see cref="FeatureFlagDisabledException"/> if the flag is disabled (evaluates to <c>false</c>).
/// When the flag is enabled, the request is passed through to the next step unchanged.
/// Register this as an <see cref="IPipelineBehavior"/> to guard an entire command or query path.
/// </summary>
/// <remarks>
/// Stateless pipeline behavior; resolved per request. Short-circuits the pipeline by throwing
/// <see cref="FeatureFlagDisabledException"/> when the kill-switch flag evaluates to true.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// </remarks>
/// <typeparam name="TRequest">The command or query type. Used to constrain typed dispatch.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed class KillSwitchBehavior<TRequest, TResult> : IPipelineBehavior
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;

    /// <summary>
    /// Initializes a new <see cref="KillSwitchBehavior{TRequest,TResult}"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the kill-switch flag.</param>
    /// <param name="flagKey">The flag key to check. If this flag is disabled, the pipeline is halted.</param>
    public KillSwitchBehavior(ISharpNinjaFeatureClient client, string flagKey)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);

        _client = client;
        _flagKey = flagKey;
    }

    /// <inheritdoc />
    public Task<Result<T>> HandleAsync<T>(object request, CallContext context, Func<Task<Result<T>>> nextStep)
    {
        var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
        if (!evaluation.Value)
        {
            throw new FeatureFlagDisabledException(_flagKey);
        }

        return nextStep();
    }
}
