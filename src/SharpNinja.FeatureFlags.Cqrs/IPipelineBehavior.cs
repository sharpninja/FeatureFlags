namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-005: Pipeline behavior that wraps handler execution with pre/post processing.
/// Behaviors receive the request, <see cref="CallContext"/>, and a continuation delegate.
/// Behaviors can short-circuit by returning <see cref="Result.Failure{T}(string)"/> without calling the continuation.
/// Registration order determines execution order (outermost first).
/// </summary>
public interface IPipelineBehavior
{
    /// <summary>
    /// Handles the pipeline step. Call <paramref name="nextStep"/> to continue to the next behavior or handler.
    /// Return a <see cref="Result{T}"/> directly to short-circuit the pipeline.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="request">The command or query being dispatched.</param>
    /// <param name="context">The call context for this dispatch.</param>
    /// <param name="nextStep">Delegate to invoke the next behavior or the handler.</param>
    /// <returns>The result from the handler or a short-circuit result.</returns>
    Task<Result<T>> HandleAsync<T>(object request, CallContext context, Func<Task<Result<T>>> nextStep);
}
