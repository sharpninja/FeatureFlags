using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Wolverine;
using Wolverine;
using Xunit;

namespace SharpNinja.FeatureFlags.Wolverine.Tests;

/// <summary>Sample Wolverine message used to exercise the middleware under test.</summary>
public sealed record PingMessage(string Payload);

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

/// <summary>Phase 6 Wolverine middleware tests for kill-switch and flag-gate gates.</summary>
public sealed class WolverineMiddlewareTests
{
    /// <summary>When the kill-switch flag is true (active), the middleware throws to halt the message.</summary>
    [Fact]
    public void KillSwitchMiddlewareThrowsWhenFlagIsTrue()
    {
        var client = new StubFeatureClient(value: true);
        const string flagKey = "kill.feature";
        var middleware = new KillSwitchMiddleware(client, flagKey);
        var context = new TestMessageContext(new PingMessage("p"));

        var ex = Assert.Throws<FeatureFlagDisabledException>(() => middleware.Before(context));
        Assert.Equal(flagKey, ex.FlagKey);
    }

    /// <summary>When the kill-switch flag is false, the middleware allows the message to proceed.</summary>
    [Fact]
    public void KillSwitchMiddlewarePassesWhenFlagIsFalse()
    {
        var client = new StubFeatureClient(value: false);
        var middleware = new KillSwitchMiddleware(client, "kill.feature");
        var context = new TestMessageContext(new PingMessage("p"));

        middleware.Before(context);
    }

    /// <summary>When the gate flag matches the required value, the middleware allows the message to proceed.</summary>
    [Fact]
    public void FlagGateMiddlewarePassesWhenFlagMatches()
    {
        var client = new StubFeatureClient(value: true);
        var middleware = new FlagGateMiddleware(client, "gate.feature", requiredValue: true);
        var context = new TestMessageContext(new PingMessage("p"));

        middleware.Before(context);
    }

    /// <summary>When the gate flag does not match the required value, the middleware throws.</summary>
    [Fact]
    public void FlagGateMiddlewareThrowsWhenFlagDoesNotMatch()
    {
        var client = new StubFeatureClient(value: false);
        const string flagKey = "gate.feature";
        var middleware = new FlagGateMiddleware(client, flagKey, requiredValue: true);
        var context = new TestMessageContext(new PingMessage("p"));

        var ex = Assert.Throws<FeatureFlagDisabledException>(() => middleware.Before(context));
        Assert.Equal(flagKey, ex.FlagKey);
    }
}
