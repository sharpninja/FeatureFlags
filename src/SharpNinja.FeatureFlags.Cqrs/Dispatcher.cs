using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-001, TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-003, TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-004: Central dispatcher for CQRS commands and queries.
/// Resolves handlers from DI, wraps execution in pipeline behaviors, manages <see cref="CallContext"/>
/// lifecycle, and implements <see cref="ILoggerProvider"/> for correlation-enriched logging.
/// </summary>
public sealed class Dispatcher : IDispatcher, ILoggerProvider
{
    private const int MaxRetainedDispatchLogs = 200;
    private static readonly Action<ILogger, string, string, Exception?> s_dispatching =
        LoggerMessage.Define<string, string>(
            LogLevel.Debug,
            new EventId(1000, "Dispatching"),
            "Dispatching {Operation} [{CorrelationId}]");

    private static readonly Action<ILogger, string, string, long, Exception?> s_dispatchCancelled =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Warning,
            new EventId(1001, "DispatchCancelled"),
            "Dispatch cancelled {Operation} [{CorrelationId}] after {ElapsedMilliseconds}ms");

    private static readonly Action<ILogger, string, string, long, Exception?> s_dispatchTimedOut =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Warning,
            new EventId(1002, "DispatchTimedOut"),
            "Dispatch timed out {Operation} [{CorrelationId}] after {ElapsedMilliseconds}ms");

    private static readonly Action<ILogger, string, string, long, Exception?> s_dispatchException =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Error,
            new EventId(1003, "DispatchException"),
            "Dispatch failed {Operation} [{CorrelationId}] after {ElapsedMilliseconds}ms");

    private static readonly Action<ILogger, string, string, long, Exception?> s_dispatchSucceeded =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Debug,
            new EventId(1004, "DispatchSucceeded"),
            "Dispatch succeeded {Operation} [{CorrelationId}] in {ElapsedMilliseconds}ms");

    private static readonly Action<ILogger, string, string, long, string?, Exception?> s_dispatchFailed =
        LoggerMessage.Define<string, string, long, string?>(
            LogLevel.Warning,
            new EventId(1005, "DispatchFailed"),
            "Dispatch failed {Operation} [{CorrelationId}] in {ElapsedMilliseconds}ms: {Error}");

    private static readonly Action<ILogger, string, string, long, string?, Exception?> s_dispatchFailedWithException =
        LoggerMessage.Define<string, string, long, string?>(
            LogLevel.Error,
            new EventId(1006, "DispatchFailedWithException"),
            "Dispatch failed {Operation} [{CorrelationId}] in {ElapsedMilliseconds}ms: {Error}");

    private readonly IServiceProvider _services;
    private readonly ILogger<Dispatcher> _logger;
    private readonly ConcurrentDictionary<long, CallContext> _activeContexts = new();
    private readonly ConcurrentQueue<DispatchLogRecord> _recentDispatches = new();

    /// <summary>Initializes a new <see cref="Dispatcher"/>.</summary>
    /// <param name="services">The DI service provider for resolving handlers and behaviors.</param>
    /// <param name="logger">Logger for dispatcher-level diagnostics.</param>
    public Dispatcher(IServiceProvider services, ILogger<Dispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>Active call contexts keyed by <see cref="CorrelationId.BaseId"/>.</summary>
    public IReadOnlyDictionary<long, CallContext> ActiveContexts => _activeContexts;

    /// <summary>
    /// Recently completed dispatch log snapshots (oldest to newest), bounded to a fixed count.
    /// </summary>
    public IReadOnlyList<DispatchLogRecord> RecentDispatches => _recentDispatches.ToArray();

    /// <summary>
    /// Dispatches a command to its handler, wrapped in pipeline behaviors.
    /// </summary>
    /// <typeparam name="TResult">The result value type.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler result.</returns>
    [RequiresDynamicCode("Runtime CQRS dispatch constructs closed generic handler types.")]
    [RequiresUnreferencedCode("Runtime CQRS dispatch requires handler metadata to remain available.")]
    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
        => DispatchAsync<TResult>(command, typeof(ICommandHandler<,>), ct);

    /// <summary>
    /// Dispatches a query to its handler, wrapped in pipeline behaviors.
    /// </summary>
    /// <typeparam name="TResult">The result value type.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler result.</returns>
    [RequiresDynamicCode("Runtime CQRS dispatch constructs closed generic handler types.")]
    [RequiresUnreferencedCode("Runtime CQRS dispatch requires handler metadata to remain available.")]
    public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
        => DispatchAsync<TResult>(query, typeof(IQueryHandler<,>), ct);

    [RequiresDynamicCode("Runtime CQRS dispatch constructs closed generic handler types.")]
    [RequiresUnreferencedCode("Runtime CQRS dispatch requires handler metadata to remain available.")]
    private async Task<Result<TResult>> DispatchAsync<TResult>(object request, Type handlerOpenType, CancellationToken ct)
    {
        var requestType = request.GetType();
        var context = new CallContext { OperationName = requestType.Name, CancellationToken = ct };
        _activeContexts[context.Correlation.BaseId] = context;
        var outcome = "Unknown";
        string? error = null;
        context.Properties["RequestData"] = request.ToYaml();

        var sw = Stopwatch.StartNew();
        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                s_dispatching(_logger, context.OperationName, context.Correlation.Current, null);
            }

            // Apply timeout if the request implements IHasTimeout
            var effectiveCt = ct;
            CancellationTokenSource? timeoutCts = null;
            if (request is IHasTimeout hasTimeout)
            {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(hasTimeout.Timeout);
                effectiveCt = timeoutCts.Token;
                context.CancellationToken = effectiveCt;
            }

            try
            {
                // Resolve handler
                var handlerType = handlerOpenType.MakeGenericType(requestType, typeof(TResult));
                var handler = _services.GetRequiredService(handlerType);

                // Build pipeline: behaviors wrap the handler invocation
                var behaviors = _services.GetServices<IPipelineBehavior>().ToList();

                // The innermost delegate calls the handler
                Func<Task<Result<TResult>>> innermost = () => InvokeHandler<TResult>(handler, handlerType, request, context);

                // Wrap with behaviors (outermost first = first registered)
                var pipeline = innermost;
                for (var i = behaviors.Count - 1; i >= 0; i--)
                {
                    var behavior = behaviors[i];
                    var next = pipeline;
                    pipeline = () => behavior.HandleAsync(request, context, next);
                }

                var result = await pipeline().ConfigureAwait(true);

                sw.Stop();
                LogResult(result, context, sw.Elapsed);
                outcome = result.IsSuccess ? "Success" : "Failure";
                error = result.Error;
                context.Properties["ResultData"] = result.ToString();
                return result;
            }
            finally
            {
                timeoutCts?.Dispose();
            }
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            var result = Result.Failure<TResult>("Operation was cancelled.", ex);
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                s_dispatchCancelled(_logger, context.OperationName, context.Correlation.Current, sw.ElapsedMilliseconds, ex);
            }

            outcome = "Cancelled";
            error = result.Error;
            context.Properties["ResultData"] = result.ToString();
            return result;
        }
        catch (OperationCanceledException ex)
        {
            sw.Stop();
            var result = Result.Failure<TResult>("Operation timed out.", ex);
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                s_dispatchTimedOut(_logger, context.OperationName, context.Correlation.Current, sw.ElapsedMilliseconds, ex);
            }

            outcome = "Timeout";
            error = result.Error;
            context.Properties["ResultData"] = result.ToString();
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (_logger.IsEnabled(LogLevel.Error))
            {
                s_dispatchException(_logger, context.OperationName, context.Correlation.Current, sw.ElapsedMilliseconds, ex);
            }

            outcome = "Exception";
            error = ex.Message;
            var result = Result.Failure<TResult>(ex);
            context.Properties["ResultData"] = result.ToString();
            return result;
        }
        finally
        {
            if (sw.IsRunning)
                sw.Stop();
            _activeContexts.TryRemove(context.Correlation.BaseId, out _);
            CaptureDispatchLog(context, sw.Elapsed, outcome, error);
            context.Dispose();
        }
    }

    private static async Task<Result<TResult>> InvokeHandler<TResult>(
        object handler,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type handlerType,
        object request,
        CallContext context)
    {
        // Use reflection to call HandleAsync(request, context)
        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"Handler {handlerType.Name} does not have a HandleAsync method.");

        var task = (Task<Result<TResult>>)method.Invoke(handler, [request, context])!;
        return await task.ConfigureAwait(true);
    }

    /// <summary>
    /// TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-004: Logs the result of a dispatch call.
    /// Success → Debug, Failure with exception → Error, Failure without exception → Warning.
    /// </summary>
    private void LogResult<TResult>(Result<TResult> result, CallContext context, TimeSpan elapsed)
    {
        var elapsedMilliseconds = (long)Math.Round(elapsed.TotalMilliseconds);
        if (result.IsSuccess)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                s_dispatchSucceeded(_logger, context.OperationName, context.Correlation.Current, elapsedMilliseconds, null);
            }
        }
        else if (result.Exception is not null)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                s_dispatchFailedWithException(_logger, context.OperationName, context.Correlation.Current, elapsedMilliseconds, result.Error, result.Exception);
            }
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                s_dispatchFailed(_logger, context.OperationName, context.Correlation.Current, elapsedMilliseconds, result.Error, null);
            }
        }
    }

    // --- ILoggerProvider implementation ---

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new DispatcherLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose() { /* No resources to dispose */ }

    private void CaptureDispatchLog(CallContext context, TimeSpan elapsed, string outcome, string? error)
    {
        var finishedAt = DateTimeOffset.UtcNow;
        var entries = context.Entries
            .OrderBy(e => e.Timestamp)
            .Select(static e => new DispatchLogRecordEntry(
                e.Timestamp,
                e.Level,
                e.Message,
                e.Exception?.GetType().FullName,
                e.Exception?.Message))
            .ToArray();

        context.Properties.TryGetValue("RequestData", out var reqObj);
        context.Properties.TryGetValue("ResultData", out var resObj);

        var record = new DispatchLogRecord(
            context.Started,
            finishedAt,
            context.OperationName,
            context.Correlation.Current,
            string.IsNullOrWhiteSpace(outcome) ? "Unknown" : outcome,
            (long)Math.Round(elapsed.TotalMilliseconds),
            error,
            context.UserId,
            context.UserName,
            context.Roles?.Where(static r => !string.IsNullOrWhiteSpace(r)).ToArray() ?? [],
            entries,
            reqObj as string,
            resObj as string);

        _recentDispatches.Enqueue(record);
        while (_recentDispatches.Count > MaxRetainedDispatchLogs && _recentDispatches.TryDequeue(out _))
        {
        }
    }
}

/// <summary>
/// TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-003: Logger created by <see cref="Dispatcher"/> as <see cref="ILoggerProvider"/>.
/// Enriches structured log entries with decomposed correlation context from active <see cref="CallContext"/>s.
/// </summary>
internal sealed class DispatcherLogger : ILogger
{
    private readonly Dispatcher _dispatcher;
    private readonly string _categoryName;

    /// <summary>Initializes a new <see cref="DispatcherLogger"/>.</summary>
    /// <param name="dispatcher">The dispatcher providing active context lookup.</param>
    /// <param name="categoryName">The logger category name.</param>
    public DispatcherLogger(Dispatcher dispatcher, string categoryName)
    {
        _dispatcher = dispatcher;
        _categoryName = categoryName;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Find the active context (if any) to enrich the log entry
        // This is a best-effort lookup — if no context is active, the log is still emitted
        foreach (var kvp in _dispatcher.ActiveContexts)
        {
            kvp.Value.Log(logLevel, eventId, state, exception, formatter);
            break; // Use the first active context (typically there's only one per thread)
        }
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
