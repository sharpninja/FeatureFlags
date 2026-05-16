using MediatR;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.MediatR;

/// <summary>
/// MediatR pipeline behavior that acts as a kill-switch. When the configured feature
/// flag evaluates to <c>true</c> (the kill-switch is active), the pipeline is halted
/// by throwing <see cref="FeatureFlagDisabledException"/> before the handler runs.
/// </summary>
/// <remarks>
/// Stateless pipeline behavior; resolved per request. Short-circuits the pipeline by throwing
/// <see cref="FeatureFlagDisabledException"/> when the kill-switch flag evaluates to true.
/// </remarks>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
public sealed class KillSwitchBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;

    /// <summary>
    /// Initializes a new <see cref="KillSwitchBehavior{TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the kill-switch flag.</param>
    /// <param name="flagKey">The flag key to check. When the flag is <c>true</c>, the pipeline is halted.</param>
    public KillSwitchBehavior(ISharpNinjaFeatureClient client, string flagKey)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);

        _client = client;
        _flagKey = flagKey;
    }

    /// <inheritdoc />
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
        if (evaluation.Value)
        {
            throw new FeatureFlagDisabledException(_flagKey);
        }

        return next(cancellationToken);
    }
}
