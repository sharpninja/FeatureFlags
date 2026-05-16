using MediatR;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.MediatR;

/// <summary>
/// MediatR pipeline behavior that, after the handler runs successfully, evaluates a
/// feature flag and logs a structured exposure event recording the flag key, the
/// request type, and the evaluation reason. Useful for experimentation pipelines
/// that need to record which flag variant was active for a given request.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
public sealed class ExposureLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Action<ILogger, string, string, string, Exception?> s_flagExposure =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(3000, "FlagExposure"),
            "Flag '{FlagKey}' was evaluated for handling of '{RequestType}'. Result: {Reason}.");

    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new <see cref="ExposureLoggingBehavior{TRequest, TResponse}"/>.
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
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
            s_flagExposure(_logger, _flagKey, typeof(TRequest).Name, evaluation.Reason.ToString(), null);
        }

        return response;
    }
}
