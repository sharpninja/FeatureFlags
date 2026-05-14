namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 TR-9 TR-10 TR-11 v1 provider-ready manifest origin store abstraction.</summary>
public interface IDistributionManifestRegistry
{
    /// <summary>TR-10 gets the number of manifests currently visible to the store.</summary>
    int Count { get; }

    /// <summary>FR-3 FR-6 finds a manifest for the requested product, release, and environment tuple.</summary>
    /// <param name="productId">Product identifier.</param>
    /// <param name="releaseId">Release identifier.</param>
    /// <param name="environment">Deployment environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching manifest, or <see langword="null"/> when none exists.</returns>
    ValueTask<DistributionManifest?> FindAsync(
        string productId,
        string releaseId,
        string environment,
        CancellationToken cancellationToken);

    /// <summary>FR-3 upserts a manifest into the origin store.</summary>
    /// <param name="manifest">Manifest to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask UpsertAsync(DistributionManifest manifest, CancellationToken cancellationToken);
}
