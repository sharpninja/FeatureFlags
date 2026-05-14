using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 FR-7 TR-6 TR-9 TR-11 v1 injectable transport boundary for fetching signed remote manifests.</summary>
public interface ISharpNinjaRemoteManifestClient
{
    /// <summary>FR-3 fetches a signed manifest envelope from the configured Distribution source.</summary>
    /// <param name="options">SDK options.</param>
    /// <param name="forceRefresh">Whether this refresh bypasses normal cache cadence for kill-switch scenarios.</param>
    /// <param name="currentETag">Current manifest ETag for conditional requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Remote manifest fetch result.</returns>
    ValueTask<RemoteManifestFetchResult> FetchAsync(
        SharpNinjaFeatureFlagOptions options,
        bool forceRefresh,
        string? currentETag,
        CancellationToken cancellationToken = default);
}
