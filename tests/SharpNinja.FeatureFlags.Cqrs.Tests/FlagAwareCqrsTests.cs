using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpNinja.FeatureFlags.Abstractions;
using Xunit;

namespace SharpNinja.FeatureFlags.Cqrs.Tests;

// ---------------------------------------------------------------------------
// Shared test fixtures
// ---------------------------------------------------------------------------

/// <summary>Test command for flag-aware routing tests.</summary>
public sealed record PingCommand(string Payload) : ICommand<string>;

/// <summary>Primary handler for PingCommand - returns "primary: {Payload}".</summary>
public sealed class PingPrimaryHandler : ICommandHandler<PingCommand, string>
{
    /// <inheritdoc />
    public Task<Result<string>> HandleAsync(PingCommand command, CallContext context)
        => Task.FromResult(Result.Success($"primary: {command.Payload}"));
}

/// <summary>Secondary handler for PingCommand - returns "secondary: {Payload}".</summary>
public sealed class PingSecondaryHandler : ICommandHandler<PingCommand, string>
{
    /// <inheritdoc />
    public Task<Result<string>> HandleAsync(PingCommand command, CallContext context)
        => Task.FromResult(Result.Success($"secondary: {command.Payload}"));
}

/// <summary>Test query for flag-aware routing tests.</summary>
public sealed record EchoQuery(string Text) : IQuery<string>;

/// <summary>Primary handler for EchoQuery.</summary>
public sealed class EchoPrimaryQueryHandler : IQueryHandler<EchoQuery, string>
{
    /// <inheritdoc />
    public Task<Result<string>> HandleAsync(EchoQuery query, CallContext context)
        => Task.FromResult(Result.Success($"primary: {query.Text}"));
}

// ---------------------------------------------------------------------------
// Stub feature client
// ---------------------------------------------------------------------------

/// <summary>
/// Simple stub that returns a predetermined boolean value for any flag key.
/// </summary>
internal sealed class StubFeatureClient : ISharpNinjaFeatureClient
{
    private readonly bool _value;
    private readonly EvaluationReason _reason;

    /// <summary>Initializes the stub with a fixed evaluation result.</summary>
    /// <param name="value">The boolean value to return.</param>
    /// <param name="reason">The reason to report.</param>
    public StubFeatureClient(bool value, EvaluationReason reason = EvaluationReason.RuleMatch)
    {
        _value = value;
        _reason = reason;
    }

    /// <inheritdoc />
    public EvaluationResult<T> Evaluate<T>(string key, T defaultValue, EvaluationContext? context = null)
    {
        if (typeof(T) == typeof(bool))
        {
            return new EvaluationResult<T>((T)(object)_value, _reason);
        }

        return new EvaluationResult<T>(defaultValue, EvaluationReason.Default);
    }

    /// <inheritdoc />
    public ValueTask<EvaluationResult<T>> EvaluateAsync<T>(
        string key,
        T defaultValue,
        EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Evaluate(key, defaultValue, context));
}

/// <summary>
/// Stub selector that records whether it was called and returns a fixed handler.
/// </summary>
internal sealed class RecordingSelector<TRequest, TResult> : IHandlerSelector<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    private readonly ICommandHandler<TRequest, TResult>? _handler;

    /// <summary>Whether <see cref="SelectCommandHandler"/> was called.</summary>
    public bool WasCalled { get; private set; }

    /// <summary>Initializes the selector with a handler to return.</summary>
    /// <param name="handler">The handler to return, or null.</param>
    public RecordingSelector(ICommandHandler<TRequest, TResult>? handler)
    {
        _handler = handler;
    }

    /// <inheritdoc />
    public ICommandHandler<TRequest, TResult>? SelectCommandHandler(TRequest request, IServiceProvider services)
    {
        WasCalled = true;
        return _handler;
    }
}

/// <summary>
/// Stub logger that captures whether any message was logged.
/// </summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly List<string> _messages = [];

    /// <summary>All logged messages in order.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _messages.Add(formatter(state, exception));

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>Phase 6.4: tests for flag-aware CQRS adapter components.</summary>
public class FlagAwareCqrsTests
{
    // -----------------------------------------------------------------------
    // 1. FlagAwareHandlerSelector - routes to enabled selector when flag is true
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the feature flag evaluates to true, the enabled selector is called
    /// and the disabled selector is not.
    /// </summary>
    [Fact]
    public void FlagAwareSelectorRoutesToEnabledHandlerWhenFlagIsTrue()
    {
        // Arrange
        var client = new StubFeatureClient(value: true);
        var primaryHandler = new PingPrimaryHandler();
        var secondaryHandler = new PingSecondaryHandler();

        var enabledSelector = new RecordingSelector<PingCommand, string>(primaryHandler);
        var disabledSelector = new RecordingSelector<PingCommand, string>(secondaryHandler);

        var selector = new FlagAwareHandlerSelector<PingCommand, string>(
            client, "my-flag", enabledSelector, disabledSelector);

        var services = new ServiceCollection().BuildServiceProvider();
        var request = new PingCommand("hello");

        // Act
        var result = selector.SelectCommandHandler(request, services);

        // Assert
        Assert.True(enabledSelector.WasCalled, "Enabled selector must be called when flag is true.");
        Assert.False(disabledSelector.WasCalled, "Disabled selector must NOT be called when flag is true.");
        Assert.Same(primaryHandler, result);
    }

    // -----------------------------------------------------------------------
    // 2. FlagAwareHandlerSelector - routes to disabled selector when flag is false
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the feature flag evaluates to false, the disabled selector is called
    /// and the enabled selector is not.
    /// </summary>
    [Fact]
    public void FlagAwareSelectorRoutesToDisabledHandlerWhenFlagIsFalse()
    {
        // Arrange
        var client = new StubFeatureClient(value: false, reason: EvaluationReason.Disabled);
        var primaryHandler = new PingPrimaryHandler();
        var secondaryHandler = new PingSecondaryHandler();

        var enabledSelector = new RecordingSelector<PingCommand, string>(primaryHandler);
        var disabledSelector = new RecordingSelector<PingCommand, string>(secondaryHandler);

        var selector = new FlagAwareHandlerSelector<PingCommand, string>(
            client, "my-flag", enabledSelector, disabledSelector);

        var services = new ServiceCollection().BuildServiceProvider();
        var request = new PingCommand("hello");

        // Act
        var result = selector.SelectCommandHandler(request, services);

        // Assert
        Assert.False(enabledSelector.WasCalled, "Enabled selector must NOT be called when flag is false.");
        Assert.True(disabledSelector.WasCalled, "Disabled selector must be called when flag is false.");
        Assert.Same(secondaryHandler, result);
    }

    // -----------------------------------------------------------------------
    // 3. FlagAwareHandlerSelector - null disabledSelector returns null when flag is false
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the flag is false and no disabled selector is provided, the selector
    /// returns null rather than throwing.
    /// </summary>
    [Fact]
    public void FlagAwareSelectorReturnsNullWhenFlagFalseAndNoDisabledSelector()
    {
        // Arrange
        var client = new StubFeatureClient(value: false);
        var enabledSelector = new RecordingSelector<PingCommand, string>(new PingPrimaryHandler());

        var selector = new FlagAwareHandlerSelector<PingCommand, string>(
            client, "my-flag", enabledSelector, disabledSelector: null);

        var services = new ServiceCollection().BuildServiceProvider();

        // Act
        var result = selector.SelectCommandHandler(new PingCommand("x"), services);

        // Assert
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // 4. KillSwitchBehavior - throws when flag is disabled
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the kill-switch flag evaluates to false (disabled/killed),
    /// KillSwitchBehavior throws FeatureFlagDisabledException before the handler runs.
    /// </summary>
    [Fact]
    public async Task KillSwitchBehaviorThrowsWhenFlagDisabled()
    {
        // Arrange
        var client = new StubFeatureClient(value: false, reason: EvaluationReason.Disabled);
        const string flagKey = "feature.kill";
        var behavior = new KillSwitchBehavior<PingCommand, string>(client, flagKey);

        var handlerCalled = false;
        var context = new CallContext(CorrelationId.Create());
        Func<Task<Result<string>>> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success("done"));
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<FeatureFlagDisabledException>(
            () => behavior.HandleAsync<string>(new PingCommand("x"), context, next));

        Assert.Equal(flagKey, ex.FlagKey);
        Assert.False(handlerCalled, "Handler must not be called when kill-switch fires.");
    }

    // -----------------------------------------------------------------------
    // 5. KillSwitchBehavior - passes through when flag is enabled
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the kill-switch flag evaluates to true (enabled),
    /// KillSwitchBehavior does not throw and passes control to the next step.
    /// </summary>
    [Fact]
    public async Task KillSwitchBehaviorPassesThroughWhenFlagEnabled()
    {
        // Arrange
        var client = new StubFeatureClient(value: true, reason: EvaluationReason.RuleMatch);
        const string flagKey = "feature.ok";
        var behavior = new KillSwitchBehavior<PingCommand, string>(client, flagKey);

        var handlerCalled = false;
        var context = new CallContext(CorrelationId.Create());
        Func<Task<Result<string>>> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success("ok"));
        };

        // Act
        var result = await behavior.HandleAsync<string>(new PingCommand("y"), context, next);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
        Assert.True(handlerCalled, "Handler must be called when kill-switch flag is enabled.");
    }

    // -----------------------------------------------------------------------
    // 6. ExposureLoggingBehavior - logs after dispatch
    // -----------------------------------------------------------------------

    /// <summary>
    /// After the handler runs successfully, ExposureLoggingBehavior logs a structured
    /// message containing the flag key and the request type name.
    /// </summary>
    [Fact]
    public async Task ExposureLoggingBehaviorLogsAfterDispatch()
    {
        // Arrange
        var client = new StubFeatureClient(value: true, reason: EvaluationReason.RuleMatch);
        const string flagKey = "exposure.flag";
        var capturingLogger = new CapturingLogger();
        var behavior = new ExposureLoggingBehavior<PingCommand, string>(client, flagKey, capturingLogger);

        var context = new CallContext(CorrelationId.Create());
        Func<Task<Result<string>>> next = () => Task.FromResult(Result.Success("logged"));

        // Act
        var result = await behavior.HandleAsync<string>(new PingCommand("z"), context, next);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(capturingLogger.Messages);

        var logMessage = capturingLogger.Messages[0];
        Assert.Contains(flagKey, logMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(PingCommand), logMessage, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // 7. FlagSnapshotBehavior - populates CallContext.FlagSnapshot
    // -----------------------------------------------------------------------

    /// <summary>
    /// Before dispatching to the next step, FlagSnapshotBehavior evaluates each
    /// requested flag key and stores the results in CallContext.FlagSnapshot.
    /// </summary>
    [Fact]
    public async Task FlagSnapshotBehaviorPopulatesCallContextSnapshot()
    {
        // Arrange
        var client = new StubFeatureClient(value: true, reason: EvaluationReason.RuleMatch);
        var flagKeys = new List<string> { "flag.alpha", "flag.beta" };
        var behavior = new FlagSnapshotBehavior<PingCommand, string>(client, flagKeys);

        CallContext? capturedContext = null;
        var context = new CallContext(CorrelationId.Create());
        Func<Task<Result<string>>> next = () =>
        {
            capturedContext = context;
            return Task.FromResult(Result.Success("snapped"));
        };

        // Act
        var result = await behavior.HandleAsync<string>(new PingCommand("snap"), context, next);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedContext);

        var snapshot = capturedContext.FlagSnapshot;
        Assert.Contains("flag.alpha", snapshot.Keys);
        Assert.Contains("flag.beta", snapshot.Keys);
        Assert.Equal(2, snapshot.Count);
    }

    // -----------------------------------------------------------------------
    // 8. DefaultHandlerSelector - resolves from DI
    // -----------------------------------------------------------------------

    /// <summary>
    /// DefaultHandlerSelector resolves a registered command handler from DI
    /// and returns it.
    /// </summary>
    [Fact]
    public void DefaultHandlerSelectorResolvesRegisteredHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ICommandHandler<PingCommand, string>, PingPrimaryHandler>();
        using var sp = services.BuildServiceProvider();

        var selector = new DefaultHandlerSelector<PingCommand, string>();

        // Act
        var handler = selector.SelectCommandHandler(new PingCommand("test"), sp);

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<PingPrimaryHandler>(handler);
    }

    /// <summary>
    /// DefaultHandlerSelector returns null when no handler is registered in DI.
    /// </summary>
    [Fact]
    public void DefaultHandlerSelectorReturnsNullWhenNotRegistered()
    {
        // Arrange
        using var sp = new ServiceCollection().BuildServiceProvider();
        var selector = new DefaultHandlerSelector<PingCommand, string>();

        // Act
        var handler = selector.SelectCommandHandler(new PingCommand("test"), sp);

        // Assert
        Assert.Null(handler);
    }

    // -----------------------------------------------------------------------
    // 9. CallContext.FlagSnapshot default value
    // -----------------------------------------------------------------------

    /// <summary>
    /// A freshly constructed CallContext has an empty FlagSnapshot dictionary.
    /// </summary>
    [Fact]
    public void CallContextFlagSnapshotDefaultsToEmpty()
    {
        // Arrange
        var context = new CallContext(CorrelationId.Create());

        // Act & Assert
        Assert.NotNull(context.FlagSnapshot);
        Assert.Empty(context.FlagSnapshot);
    }

    // -----------------------------------------------------------------------
    // 10. DefaultHandlerInvoker - invokes command and query handlers
    // -----------------------------------------------------------------------

    /// <summary>
    /// DefaultHandlerInvoker.InvokeCommandAsync delegates to the handler's HandleAsync method.
    /// </summary>
    [Fact]
    public async Task DefaultHandlerInvokerInvokesCommandHandler()
    {
        // Arrange
        var invoker = new DefaultHandlerInvoker<PingCommand, string>();
        var handler = new PingPrimaryHandler();
        var request = new PingCommand("invoke-me");

        // Act
        var result = await invoker.InvokeCommandAsync(handler, request, CancellationToken.None);

        // Assert
        Assert.Equal("primary: invoke-me", result);
    }

    /// <summary>
    /// DefaultQueryHandlerInvoker.InvokeQueryAsync delegates to the query handler's HandleAsync method.
    /// </summary>
    [Fact]
    public async Task DefaultQueryHandlerInvokerInvokesQueryHandler()
    {
        // Arrange
        var invoker = new DefaultQueryHandlerInvoker<EchoQuery, string>();
        var handler = new EchoPrimaryQueryHandler();
        var request = new EchoQuery("invoke-query");

        // Act
        var result = await invoker.InvokeQueryAsync(handler, request, CancellationToken.None);

        // Assert
        Assert.Equal("primary: invoke-query", result);
    }
}
