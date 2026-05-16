using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// A pipeline behavior that evaluates a feature flag after the handler runs successfully
/// and logs a structured exposure event recording the flag key, request type, and evaluation reason.
/// This supports experimentation pipelines that need to track which flag variant was active
/// when a given command or query was processed.
/// </summary>
/// <typeparam name="TRequest">The command or query type. Used for the log message.</typeparam>
/// <typeparam name="TResult">The result value type.</typeparam>
public sealed class ExposureLoggingBehavior<TRequest, TResult> : IPipelineBehavior
{
    private static readonly Action<ILogger, string, string, string, Exception?> s_flagExposure =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(2000, "FlagExposure"),
            "Flag '{FlagKey}' was evaluated for dispatch of '{RequestType}'. Result: {Reason}.");

    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new <see cref="ExposureLoggingBehavior{TRequest,TResult}"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the flag.</param>
    /// <param name="flagKey">The flag key to evaluate and log.</param>
    /// <param name="logger">The logger that receives the exposure event.</param>
    public ExposureLoggingBehavior(ISharpNinjaFeatureClient client, string flagKey, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _flagKey = flagKey;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<T>> HandleAsync<T>(object request, CallContext context, Func<Task<Result<T>>> nextStep)
    {
        var result = await nextStep().ConfigureAwait(false);

        if (result.IsSuccess && _logger.IsEnabled(LogLevel.Information))
        {
            var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
            s_flagExposure(_logger, _flagKey, typeof(TRequest).Name, evaluation.Reason.ToString(), null);
        }

        return result;
    }
}
