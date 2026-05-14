using System.Text.Json;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-8 TR-10 v1 immutable exposure event after Distribution service acceptance.</summary>
/// <param name="ProductId">Product identifier that emitted the event.</param>
/// <param name="ReleaseId">Release identifier that emitted the event.</param>
/// <param name="Environment">Deployment environment that emitted the event.</param>
/// <param name="FlagKey">Evaluated flag key.</param>
/// <param name="ResolvedValue">Resolved flag value.</param>
/// <param name="MatchedRuleIndex">Optional zero-based rule index that matched.</param>
/// <param name="ContextFingerprint">Stable evaluation context fingerprint.</param>
/// <param name="Timestamp">UTC timestamp captured by the SDK.</param>
/// <param name="ReceivedAt">UTC timestamp captured by the Distribution service.</param>
public sealed record StoredExposureEvent(
    string ProductId,
    string ReleaseId,
    string Environment,
    string FlagKey,
    JsonElement ResolvedValue,
    int? MatchedRuleIndex,
    string ContextFingerprint,
    DateTimeOffset Timestamp,
    DateTimeOffset ReceivedAt);
