namespace SharpNinja.FeatureFlags.Distribution;

internal interface IDistributionManifestRegistry
{
    int Count { get; }

    ValueTask<DistributionManifest?> FindAsync(
        string productId,
        string releaseId,
        string environment,
        CancellationToken cancellationToken);

    ValueTask UpsertAsync(DistributionManifest manifest, CancellationToken cancellationToken);
}
