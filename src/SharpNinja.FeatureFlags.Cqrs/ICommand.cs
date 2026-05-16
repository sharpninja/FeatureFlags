namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: marker interface for CQRS commands (mutations).
/// Commands represent intent to change state and return a <see cref="Result{TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result value on success.</typeparam>
public interface ICommand<TResult> { }
