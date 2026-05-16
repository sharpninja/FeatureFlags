namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-5 TR-7 v1 contract: immutable exposure event emitted by SDK evaluations.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-5"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// </remarks>
/// <param name="FlagKey">Evaluated feature flag key.</param>
/// <param name="ResolvedValue">Resolved feature flag value.</param>
/// <param name="Reason">Evaluation reason.</param>
/// <param name="RuleIndex">Optional zero-based index of the matched rule.</param>
/// <param name="ContextFingerprint">Stable fingerprint of the effective evaluation context.</param>
/// <param name="Timestamp">UTC timestamp captured when the evaluation completed.</param>
/// <param name="ProductId">Immutable product identifier.</param>
/// <param name="ReleaseId">Immutable release identifier.</param>
/// <param name="Environment">Deployment environment.</param>
/// <param name="TenantId">Optional tenant identifier.</param>
public sealed record SharpNinjaExposureEvent(
    string FlagKey,
    object? ResolvedValue,
    EvaluationReason Reason,
    int? RuleIndex,
    string ContextFingerprint,
    DateTimeOffset Timestamp,
    string ProductId,
    string ReleaseId,
    string Environment,
    string? TenantId);
