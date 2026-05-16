using SharpNinja.FeatureFlags.Abstractions;
using Wolverine;

namespace SharpNinja.FeatureFlags.Wolverine;

/// <summary>
/// Wolverine middleware that acts as a kill-switch. When the configured feature flag
/// evaluates to <c>true</c> (kill-switch active), <see cref="Before(IMessageContext)"/>
/// throws <see cref="FeatureFlagDisabledException"/> to prevent the handler from running.
/// </summary>
/// <remarks>
/// Stateless Wolverine middleware. Short-circuits the message by throwing
/// <see cref="FeatureFlagDisabledException"/> when the kill-switch flag evaluates to true.
/// </remarks>
public sealed class KillSwitchMiddleware
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;

    /// <summary>
    /// Initializes a new <see cref="KillSwitchMiddleware"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the kill-switch flag.</param>
    /// <param name="flagKey">The flag key to check. When the flag is <c>true</c>, the handler is halted.</param>
    public KillSwitchMiddleware(ISharpNinjaFeatureClient client, string flagKey)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);

        _client = client;
        _flagKey = flagKey;
    }

    /// <summary>
    /// Wolverine "before" hook invoked prior to the wrapped handler. Throws
    /// <see cref="FeatureFlagDisabledException"/> when the kill-switch flag is active.
    /// </summary>
    /// <param name="context">The Wolverine message context for the current message.</param>
    public void Before(IMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
        if (evaluation.Value)
        {
            throw new FeatureFlagDisabledException(_flagKey);
        }
    }
}
