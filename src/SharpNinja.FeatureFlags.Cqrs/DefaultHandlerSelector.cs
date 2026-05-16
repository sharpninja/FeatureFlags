using Microsoft.Extensions.DependencyInjection;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// Default <see cref="IHandlerSelector{TRequest,TResult}"/> implementation that resolves
/// a command handler from the DI service provider using
/// <see cref="ServiceProviderServiceExtensions.GetService{T}"/>.
/// Returns <c>null</c> if no handler of the requested type is registered.
/// This replaces the reflection-based handler lookup in the <see cref="Dispatcher"/>
/// and is safe for use in AOT/trimmed applications when handlers are registered explicitly.
/// </summary>
/// <typeparam name="TRequest">The command type. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed record class DefaultHandlerSelector<TRequest, TResult> : IHandlerSelector<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    /// <inheritdoc />
    public ICommandHandler<TRequest, TResult>? SelectCommandHandler(TRequest request, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService<ICommandHandler<TRequest, TResult>>();
    }
}

/// <summary>
/// Default <see cref="IQueryHandlerSelector{TRequest,TResult}"/> implementation that resolves
/// a query handler from the DI service provider using
/// <see cref="ServiceProviderServiceExtensions.GetService{T}"/>.
/// Returns <c>null</c> if no handler of the requested type is registered.
/// </summary>
/// <typeparam name="TRequest">The query type. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed record class DefaultQueryHandlerSelector<TRequest, TResult> : IQueryHandlerSelector<TRequest, TResult>
    where TRequest : IQuery<TResult>
{
    /// <inheritdoc />
    public IQueryHandler<TRequest, TResult>? SelectQueryHandler(TRequest request, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService<IQueryHandler<TRequest, TResult>>();
    }
}
