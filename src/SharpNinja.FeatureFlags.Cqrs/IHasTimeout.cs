namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// out-of-v1: optional interface for commands/queries that specify a timeout.
/// When implemented, the <see cref="Dispatcher"/> wraps handler execution in a
/// <see cref="CancellationTokenSource"/> with the specified timeout.
/// </summary>
public interface IHasTimeout
{
    /// <summary>Maximum time allowed for handler execution.</summary>
    TimeSpan Timeout { get; }
}
