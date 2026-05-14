namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 FR-7 TR-6 TR-10 TR-11 v1 status values for SDK manifest refresh attempts.</summary>
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
/// <param name="Status">Refresh status.</param>
/// <param name="ManifestId">Active or attempted manifest identifier.</param>
/// <param name="ErrorMessage">Optional refresh failure or rejection detail.</param>
/// <param name="RefreshedAt">Timestamp for the refresh result.</param>
public sealed record ManifestRefreshResult(
    ManifestRefreshStatus Status,
    string? ManifestId = null,
    string? ErrorMessage = null,
    DateTimeOffset? RefreshedAt = null);
