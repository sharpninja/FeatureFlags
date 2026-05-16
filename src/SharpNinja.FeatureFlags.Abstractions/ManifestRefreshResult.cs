namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 FR-7 TR-6 TR-10 TR-11 v1 status values for SDK manifest refresh attempts.</summary>
/// <remarks>
/// Members carry stable ordinal values that consumers may persist; treat the enumeration as part of the public contract.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public enum ManifestRefreshStatus
{
    /// <summary>Refresh was skipped because no remote manifest endpoint is configured.</summary>
    NotConfigured,

    /// <summary>The remote source reported that the current manifest is still valid.</summary>
    Unchanged,

    /// <summary>A newer manifest was fetched, verified, cached, and activated.</summary>
    Updated,

    /// <summary>The fetched manifest was rejected and the previous active manifest was retained.</summary>
    Rejected,

    /// <summary>The refresh attempt failed before a manifest could be activated.</summary>
    Failed,
}

/// <summary>FR-3 FR-7 TR-6 TR-10 TR-11 v1 immutable result for SDK manifest refresh operations.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
/// <param name="Status">Refresh status.</param>
/// <param name="ManifestId">Active or attempted manifest identifier.</param>
/// <param name="ErrorMessage">Optional refresh failure or rejection detail.</param>
/// <param name="RefreshedAt">Timestamp for the refresh result.</param>
public sealed record ManifestRefreshResult(
    ManifestRefreshStatus Status,
    string? ManifestId = null,
    string? ErrorMessage = null,
    DateTimeOffset? RefreshedAt = null);
