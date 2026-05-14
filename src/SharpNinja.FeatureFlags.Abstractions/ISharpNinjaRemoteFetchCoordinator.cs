namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 FR-7 TR-6 TR-10 TR-11 v1 orchestrates remote refresh, signature verification, cache persistence, and activation.</summary>
public interface ISharpNinjaRemoteFetchCoordinator
{
    /// <summary>FR-3 FR-7 TR-10 gets the most recent remote refresh result.</summary>
    ManifestRefreshResult LastRefreshResult { get; }

    /// <summary>FR-3 requests a normal manifest refresh.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Manifest refresh result.</returns>
    ValueTask<ManifestRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>FR-7 requests a forced refresh for kill-switch scenarios.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Manifest refresh result.</returns>
    ValueTask<ManifestRefreshResult> ForceRefreshAsync(CancellationToken cancellationToken = default);
}
