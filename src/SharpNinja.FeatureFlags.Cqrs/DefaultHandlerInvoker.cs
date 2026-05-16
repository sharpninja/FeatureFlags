namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// Default <see cref="IHandlerInvoker{TRequest,TResult}"/> implementation that invokes the
/// command handler's <c>HandleAsync</c> method directly without reflection.
/// If the handler returns a failed <see cref="Result{TResult}"/>, an
/// <see cref="InvalidOperationException"/> is thrown carrying the failure message.
/// </summary>
/// <typeparam name="TRequest">The command type. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed record class DefaultHandlerInvoker<TRequest, TResult> : IHandlerInvoker<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    /// <inheritdoc />
    public async ValueTask<TResult> InvokeCommandAsync(
        ICommandHandler<TRequest, TResult> handler,
        TRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        // Create a minimal call context for direct invocation.
        using var context = new CallContext(CorrelationId.Create()) { CancellationToken = ct };
        var result = await handler.HandleAsync(request, context).ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw result.Exception is not null
                ? new InvalidOperationException(result.Error, result.Exception)
                : new InvalidOperationException(result.Error);
        }

        return result.Value!;
    }
}

/// <summary>
/// Default <see cref="IQueryHandlerInvoker{TRequest,TResult}"/> implementation that invokes
/// a query handler's <c>HandleAsync</c> method directly without reflection.
/// If the handler returns a failed <see cref="Result{TResult}"/>, an
/// <see cref="InvalidOperationException"/> is thrown carrying the failure message.
/// </summary>
/// <typeparam name="TRequest">The query type. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed record class DefaultQueryHandlerInvoker<TRequest, TResult> : IQueryHandlerInvoker<TRequest, TResult>
    where TRequest : IQuery<TResult>
{
    /// <inheritdoc />
    public async ValueTask<TResult> InvokeQueryAsync(
        IQueryHandler<TRequest, TResult> handler,
        TRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        using var context = new CallContext(CorrelationId.Create()) { CancellationToken = ct };
        var result = await handler.HandleAsync(request, context).ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw result.Exception is not null
                ? new InvalidOperationException(result.Error, result.Exception)
                : new InvalidOperationException(result.Error);
        }

        return result.Value!;
    }
}
