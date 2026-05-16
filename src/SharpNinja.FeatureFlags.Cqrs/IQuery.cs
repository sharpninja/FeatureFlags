namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: marker interface for CQRS queries (reads).
/// Queries represent intent to read state without side effects and return a <see cref="Result{TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result value on success.</typeparam>
public interface IQuery<TResult> { }
