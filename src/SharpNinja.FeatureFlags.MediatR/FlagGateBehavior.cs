using MediatR;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.MediatR;

/// <summary>
/// MediatR pipeline behavior that gates handler execution on a feature flag value.
/// If the flag evaluation does not match the required value, the pipeline is halted
/// by throwing <see cref="FeatureFlagDisabledException"/>.
/// </summary>
/// <remarks>
/// Stateless pipeline behavior; resolved per request. Short-circuits the pipeline by throwing
/// <see cref="FeatureFlagDisabledException"/> when the gated flag evaluates to false.
/// </remarks>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
public sealed class FlagGateBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;
    private readonly bool _requiredValue;

    /// <summary>
    /// Initializes a new <see cref="FlagGateBehavior{TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the gate flag.</param>
    /// <param name="flagKey">The flag key to evaluate.</param>
    /// <param name="requiredValue">The required boolean value for the gate to allow the request through.</param>
    public FlagGateBehavior(ISharpNinjaFeatureClient client, string flagKey, bool requiredValue)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);

        _client = client;
        _flagKey = flagKey;
        _requiredValue = requiredValue;
    }

    /// <inheritdoc />
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
        if (evaluation.Value != _requiredValue)
        {
            throw new FeatureFlagDisabledException(_flagKey);
        }

        return next(cancellationToken);
    }
}
