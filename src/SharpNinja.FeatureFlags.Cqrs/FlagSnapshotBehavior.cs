using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// FR-12 TR-11: Pipeline behavior that snapshots a configured set of feature flag evaluations
/// into <see cref="CallContext.FlagSnapshot"/> before invoking the next step in the pipeline.
/// Downstream handlers and behaviors read a stable view of flag state for the duration of the dispatch.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed class FlagSnapshotBehavior<TRequest, TResult> : IPipelineBehavior
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly IReadOnlyList<string> _flagKeys;

    /// <summary>Initializes a new <see cref="FlagSnapshotBehavior{TRequest,TResult}"/>.</summary>
    /// <param name="client">The feature flag client used to evaluate the flag keys.</param>
    /// <param name="flagKeys">The flag keys to snapshot per dispatch.</param>
    public FlagSnapshotBehavior(ISharpNinjaFeatureClient client, IEnumerable<string> flagKeys)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(flagKeys);

        _client = client;
        _flagKeys = [.. flagKeys];
    }

    /// <inheritdoc />
    public Task<Result<T>> HandleAsync<T>(object request, CallContext context, Func<Task<Result<T>>> nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        foreach (var key in _flagKeys)
        {
            var evaluation = _client.Evaluate(key, defaultValue: false);
            context.FlagSnapshot[key] = evaluation.Value;
        }

        return nextStep();
    }
}
