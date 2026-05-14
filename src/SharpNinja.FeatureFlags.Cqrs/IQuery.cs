namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-001: Marker interface for CQRS queries (reads).
/// Queries represent intent to read state without side effects and return a <see cref="Result{TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result value on success.</typeparam>
public interface IQuery<TResult> { }
