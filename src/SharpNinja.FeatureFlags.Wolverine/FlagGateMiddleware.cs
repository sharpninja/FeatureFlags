using SharpNinja.FeatureFlags.Abstractions;
using Wolverine;

namespace SharpNinja.FeatureFlags.Wolverine;

/// <summary>
/// Wolverine middleware that gates execution of a message handler on a feature flag value.
/// If the configured flag does not evaluate to the required value, <see cref="Before(IMessageContext)"/>
/// throws <see cref="FeatureFlagDisabledException"/> to halt processing of the message.
/// </summary>
/// <remarks>
/// Stateless Wolverine middleware. Short-circuits the message by throwing
/// <see cref="FeatureFlagDisabledException"/> when the gated flag evaluates to false.
/// </remarks>
public sealed class FlagGateMiddleware
{
    private readonly ISharpNinjaFeatureClient _client;
    private readonly string _flagKey;
    private readonly bool _requiredValue;

    /// <summary>
    /// Initializes a new <see cref="FlagGateMiddleware"/>.
    /// </summary>
    /// <param name="client">The feature flag client used to evaluate the gate flag.</param>
    /// <param name="flagKey">The flag key to evaluate.</param>
    /// <param name="requiredValue">The required boolean value for the gate to allow the message through.</param>
    public FlagGateMiddleware(ISharpNinjaFeatureClient client, string flagKey, bool requiredValue)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);

        _client = client;
        _flagKey = flagKey;
        _requiredValue = requiredValue;
    }

    /// <summary>
    /// Wolverine "before" hook invoked prior to the wrapped handler. Throws
    /// <see cref="FeatureFlagDisabledException"/> when the gate flag does not
    /// match the required value.
    /// </summary>
    /// <param name="context">The Wolverine message context for the current message.</param>
    public void Before(IMessageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var evaluation = _client.Evaluate(_flagKey, defaultValue: false);
        if (evaluation.Value != _requiredValue)
        {
            throw new FeatureFlagDisabledException(_flagKey);
        }
    }
}
