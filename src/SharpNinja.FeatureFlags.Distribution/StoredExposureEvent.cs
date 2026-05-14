using System.Text.Json;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed record StoredExposureEvent(
    string ProductId,
    string ReleaseId,
    string Environment,
    string FlagKey,
    JsonElement ResolvedValue,
    int? MatchedRuleIndex,
    string ContextFingerprint,
    DateTimeOffset Timestamp,
    DateTimeOffset ReceivedAt);
