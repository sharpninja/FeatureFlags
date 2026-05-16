namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: invokes a command handler without reflection, enabling AOT-safe dispatch.
/// Implementations wrap the handler's <c>HandleAsync</c> method directly so no
/// <c>MethodInfo.Invoke</c> calls are required at runtime.
/// </summary>
/// <typeparam name="TRequest">The command type. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public interface IHandlerInvoker<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    /// <summary>
    /// Invokes a command handler asynchronously without reflection.
    /// </summary>
    /// <param name="handler">The command handler to invoke.</param>
    /// <param name="request">The command to pass to the handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler's result value.</returns>
    ValueTask<TResult> InvokeCommandAsync(ICommandHandler<TRequest, TResult> handler, TRequest request, CancellationToken ct);
}

/// <summary>
/// out-of-v1: invokes a query handler without reflection, enabling AOT-safe dispatch over the query path.
/// </summary>
/// <typeparam name="TRequest">The query type. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public interface IQueryHandlerInvoker<TRequest, TResult>
    where TRequest : IQuery<TResult>
{
    /// <summary>
    /// Invokes a query handler asynchronously without reflection.
    /// </summary>
    /// <param name="handler">The query handler to invoke.</param>
    /// <param name="request">The query to pass to the handler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler's result value.</returns>
    ValueTask<TResult> InvokeQueryAsync(IQueryHandler<TRequest, TResult> handler, TRequest request, CancellationToken ct);
}
