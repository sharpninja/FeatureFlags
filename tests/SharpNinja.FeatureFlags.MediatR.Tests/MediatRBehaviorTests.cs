using MediatR;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.MediatR;
using Xunit;

namespace SharpNinja.FeatureFlags.MediatR.Tests;

/// <summary>Sample MediatR request used to exercise the pipeline behaviors under test.</summary>
public sealed record PingRequest(string Payload) : IRequest<string>;

/// <summary>
/// Stub feature client that returns a predetermined boolean value for every flag key.
/// Cribbed from the CQRS test pattern; intentionally not shared across test assemblies.
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

/// <summary>Stub logger that captures formatted log messages.</summary>
internal sealed class CapturingLogger : ILogger
{
    private readonly List<string> _messages = [];

    /// <summary>All logged messages in dispatch order.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _messages.Add(formatter(state, exception));
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}

/// <summary>Phase 6 MediatR adapter behavior tests for kill-switch, gate, and exposure logging.</summary>
public sealed class MediatRBehaviorTests
{
    /// <summary>When the kill-switch flag is true (active), the pipeline halts without invoking the handler.</summary>
    [Fact]
    public async Task KillSwitchBehaviorThrowsWhenFlagIsTrue()
    {
        var client = new StubFeatureClient(value: true);
        const string flagKey = "kill.feature";
        var behavior = new KillSwitchBehavior<PingRequest, string>(client, flagKey);

        bool nextCalled = false;
        RequestHandlerDelegate<string> next = (_) =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        var ex = await Assert.ThrowsAsync<FeatureFlagDisabledException>(
            () => behavior.Handle(new PingRequest("p"), next, CancellationToken.None));

        Assert.Equal(flagKey, ex.FlagKey);
        Assert.False(nextCalled, "Handler must not run when kill-switch is active.");
    }

    /// <summary>When the kill-switch flag is false, the pipeline runs through to the handler.</summary>
    [Fact]
    public async Task KillSwitchBehaviorPassesThroughWhenFlagIsFalse()
    {
        var client = new StubFeatureClient(value: false);
        var behavior = new KillSwitchBehavior<PingRequest, string>(client, "kill.feature");

        bool nextCalled = false;
        RequestHandlerDelegate<string> next = (_) =>
        {
            nextCalled = true;
            return Task.FromResult("done");
        };

        var result = await behavior.Handle(new PingRequest("p"), next, CancellationToken.None);

        Assert.Equal("done", result);
        Assert.True(nextCalled);
    }

    /// <summary>When the gate flag matches the required value, the pipeline runs to completion.</summary>
    [Fact]
    public async Task FlagGateBehaviorPassesThroughWhenFlagMatches()
    {
        var client = new StubFeatureClient(value: true);
        var behavior = new FlagGateBehavior<PingRequest, string>(client, "gate.feature", requiredValue: true);

        RequestHandlerDelegate<string> next = (_) => Task.FromResult("allowed");

        var result = await behavior.Handle(new PingRequest("p"), next, CancellationToken.None);

        Assert.Equal("allowed", result);
    }

    /// <summary>When the gate flag does not match the required value, the pipeline throws.</summary>
    [Fact]
    public async Task FlagGateBehaviorThrowsWhenFlagDoesNotMatch()
    {
        var client = new StubFeatureClient(value: false);
        const string flagKey = "gate.feature";
        var behavior = new FlagGateBehavior<PingRequest, string>(client, flagKey, requiredValue: true);

        bool nextCalled = false;
        RequestHandlerDelegate<string> next = (_) =>
        {
            nextCalled = true;
            return Task.FromResult("nope");
        };

        var ex = await Assert.ThrowsAsync<FeatureFlagDisabledException>(
            () => behavior.Handle(new PingRequest("p"), next, CancellationToken.None));

        Assert.Equal(flagKey, ex.FlagKey);
        Assert.False(nextCalled);
    }

    /// <summary>After the handler runs successfully, the exposure behavior records a structured log entry.</summary>
    [Fact]
    public async Task ExposureLoggingBehaviorLogsAfterHandlerCompletes()
    {
        var client = new StubFeatureClient(value: true, reason: EvaluationReason.RuleMatch);
        const string flagKey = "exposure.feature";
        var logger = new CapturingLogger();
        var behavior = new ExposureLoggingBehavior<PingRequest, string>(client, flagKey, logger);

        RequestHandlerDelegate<string> next = (_) => Task.FromResult("ran");

        var result = await behavior.Handle(new PingRequest("p"), next, CancellationToken.None);

        Assert.Equal("ran", result);
        Assert.NotEmpty(logger.Messages);
        Assert.Contains(flagKey, logger.Messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(PingRequest), logger.Messages[0], StringComparison.OrdinalIgnoreCase);
    }
}
