namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>TR-10 Phase 0 contract: diagnostic state exposed for host debug menus.</summary>
/// <param name="ProductId">Compile-time product identifier.</param>
/// <param name="ReleaseId">Compile-time release identifier.</param>
/// <param name="ManifestId">Active manifest identifier.</param>
/// <param name="LastUpdated">Last manifest update timestamp.</param>
public sealed record DiagnosticSnapshot(
    string ProductId,
    string ReleaseId,
    string ManifestId,
    DateTimeOffset? LastUpdated)
{
    /// <summary>FR-3 FR-7 TR-10 v1 diagnostic status for the most recent remote refresh.</summary>
    public ManifestRefreshStatus? LastRefreshStatus { get; init; }

    /// <summary>FR-3 FR-7 TR-10 v1 diagnostic error from the most recent remote refresh, when present.</summary>
    public string? LastRefreshError { get; init; }

    /// <summary>FR-8 TR-7 TR-10 v1 diagnostic count of exposure events pending upload.</summary>
    public int PendingExposureCount { get; init; }

    /// <summary>FR-8 TR-7 TR-10 v1 diagnostic timestamp of the last successful exposure upload.</summary>
    public DateTimeOffset? LastExposureUpload { get; init; }
}
