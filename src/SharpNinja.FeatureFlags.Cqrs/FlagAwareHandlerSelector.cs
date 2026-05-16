using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: an <see cref="IHandlerSelector{TRequest,TResult}"/> that evaluates a feature flag
/// and routes the request to one of two underlying selectors based on the result.
/// When the flag evaluates to <c>true</c> the <c>enabledSelector</c> is used;
/// when it evaluates to <c>false</c> the <c>disabledSelector</c> is used (or
/// <c>null</c> is returned if no disabled selector was provided).
/// </summary>
/// <typeparam name="TRequest">The command type. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed class FlagAwareHandlerSelector<TRequest, TResult> : IHandlerSelector<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;
    private readonly IHandlerSelector<TRequest, TResult> _enabledSelector;
    private readonly IHandlerSelector<TRequest, TResult>? _disabledSelector;

    /// <summary>
    /// Initializes a new <see cref="FlagAwareHandlerSelector{TRequest,TResult}"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the flag.</param>
    /// <param name="flagKey">The flag key to evaluate.</param>
    /// <param name="enabledSelector">Selector used when the flag evaluates to <c>true</c>.</param>
    /// <param name="disabledSelector">
    /// Selector used when the flag evaluates to <c>false</c>.
    /// Pass <c>null</c> to return <c>null</c> when the flag is disabled.
    /// </param>
    public FlagAwareHandlerSelector(
        ISharpNinjaFeatureClient client,
        string flagKey,
        IHandlerSelector<TRequest, TResult> enabledSelector,
        IHandlerSelector<TRequest, TResult>? disabledSelector)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        ArgumentNullException.ThrowIfNull(enabledSelector);

        _client = client;
        _flagKey = flagKey;
        _enabledSelector = enabledSelector;
        _disabledSelector = disabledSelector;
    }

    /// <inheritdoc />
    public ICommandHandler<TRequest, TResult>? SelectCommandHandler(TRequest request, IServiceProvider services)
    {
        var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
        return evaluation.Value
            ? _enabledSelector.SelectCommandHandler(request, services)
            : _disabledSelector?.SelectCommandHandler(request, services);
    }
}
