namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// Selects the appropriate command handler for a request, enabling flag-aware variant routing.
/// Implementations may resolve from DI, evaluate feature flags, or apply any other
/// selection strategy before returning a handler instance (or null if none is applicable).
/// </summary>
/// <typeparam name="TRequest">The command type. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public interface IHandlerSelector<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    /// <summary>
    /// Selects a command handler for the given request.
    /// Returns <c>null</c> if no handler should be used for this request.
    /// </summary>
    /// <param name="request">The command being dispatched.</param>
    /// <param name="services">The DI service provider for resolving registered handlers.</param>
    /// <returns>A command handler instance, or <c>null</c>.</returns>
    ICommandHandler<TRequest, TResult>? SelectCommandHandler(TRequest request, IServiceProvider services);
}

/// <summary>
/// Selects the appropriate query handler for a request, enabling flag-aware variant routing
/// over the query dispatch path.
/// </summary>
/// <typeparam name="TRequest">The query type. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public interface IQueryHandlerSelector<TRequest, TResult>
    where TRequest : IQuery<TResult>
{
    /// <summary>
    /// Selects a query handler for the given request.
    /// Returns <c>null</c> if no handler should be used for this request.
    /// </summary>
    /// <param name="request">The query being dispatched.</param>
    /// <param name="services">The DI service provider for resolving registered handlers.</param>
    /// <returns>A query handler instance, or <c>null</c>.</returns>
    IQueryHandler<TRequest, TResult>? SelectQueryHandler(TRequest request, IServiceProvider services);
}
