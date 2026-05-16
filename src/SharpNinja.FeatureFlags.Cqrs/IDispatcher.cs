using System.Diagnostics.CodeAnalysis;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: abstraction over <see cref="Dispatcher"/> for dispatching CQRS commands and queries.
/// Use this interface for dependency injection in consumer code and for mock-based unit testing.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Dispatches a command to its handler, wrapped in pipeline behaviors.
    /// </summary>
    /// <typeparam name="TResult">The result value type.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler result.</returns>
    [RequiresDynamicCode("Runtime CQRS dispatch constructs closed generic handler types.")]
    [RequiresUnreferencedCode("Runtime CQRS dispatch requires handler metadata to remain available.")]
    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);

    /// <summary>
    /// Dispatches a query to its handler, wrapped in pipeline behaviors.
    /// </summary>
    /// <typeparam name="TResult">The result value type.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler result.</returns>
    [RequiresDynamicCode("Runtime CQRS dispatch constructs closed generic handler types.")]
    [RequiresUnreferencedCode("Runtime CQRS dispatch requires handler metadata to remain available.")]
    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
}
