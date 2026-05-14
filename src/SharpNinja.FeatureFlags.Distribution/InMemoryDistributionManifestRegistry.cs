using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class InMemoryDistributionManifestRegistry : IDistributionManifestRegistry
{
    private static readonly Action<ILogger, string, string, string, string, Exception?> ManifestRegistered =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            new EventId(1, nameof(ManifestRegistered)),
            "Registered manifest {ProductId}/{ReleaseId}/{Environment} with ETag {ETag}.");

    private readonly ConcurrentDictionary<ManifestRegistryKey, DistributionManifest> manifests = [];
    private readonly ILogger<InMemoryDistributionManifestRegistry> logger;

    public InMemoryDistributionManifestRegistry(
        IOptions<SharpNinjaDistributionOptions> options,
        ILogger<InMemoryDistributionManifestRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _ = options.Value;
        this.logger = logger;
    }

    public int Count => manifests.Count;

    public ValueTask<DistributionManifest?> FindAsync(
        string productId,
        string releaseId,
        string environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ManifestRegistryKey key = new(productId, releaseId, environment);
        manifests.TryGetValue(key, out DistributionManifest? manifest);
        return ValueTask.FromResult(manifest);
    }

    public ValueTask UpsertAsync(DistributionManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        ManifestRegistryKey key = new(manifest.ProductId, manifest.ReleaseId, manifest.Environment);
        manifests[key] = manifest;
        ManifestRegistered(
            logger,
            manifest.ProductId,
            manifest.ReleaseId,
            manifest.Environment,
            manifest.ETag,
            null);

        return ValueTask.CompletedTask;
    }

    private readonly record struct ManifestRegistryKey(
        string ProductId,
        string ReleaseId,
        string Environment);
}
