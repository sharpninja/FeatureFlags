using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class FileBackedDistributionManifestRegistry : IDistributionManifestRegistry
{
    private static readonly Action<ILogger, string, string, string, string, Exception?> ManifestPersisted =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            new EventId(4010, nameof(ManifestPersisted)),
            "Persisted manifest {ProductId}/{ReleaseId}/{Environment} to {ManifestPath}.");

    private static readonly Action<ILogger, string, Exception?> ManifestSkipped =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4011, nameof(ManifestSkipped)),
            "Skipped invalid manifest file {ManifestPath}.");

    private readonly ConcurrentDictionary<ManifestRegistryKey, DistributionManifest> manifests = [];
    private readonly string manifestDirectory;
    private readonly ILogger<FileBackedDistributionManifestRegistry> logger;

    public FileBackedDistributionManifestRegistry(
        IOptions<SharpNinjaDistributionOptions> options,
        ILogger<FileBackedDistributionManifestRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        manifestDirectory = Path.Combine(options.Value.StorageRootPath, "manifests");
        this.logger = logger;
        LoadExistingManifests();
    }

    public int Count => manifests.Count;

    public async ValueTask<DistributionManifest?> FindAsync(
        string productId,
        string releaseId,
        string environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ManifestRegistryKey key = new(productId, releaseId, environment);
        if (manifests.TryGetValue(key, out DistributionManifest? manifest))
        {
            return manifest;
        }

        string path = GetManifestPath(productId, releaseId, environment);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        DateTimeOffset updatedAt = File.GetLastWriteTimeUtc(path);
        manifest = DistributionManifest.FromJson(json, updatedAt);
        manifests[key] = manifest;
        return manifest;
    }

    public async ValueTask UpsertAsync(DistributionManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(manifestDirectory);
        string path = GetManifestPath(manifest.ProductId, manifest.ReleaseId, manifest.Environment);
        string tempPath = string.Concat(path, ".", Guid.NewGuid().ToString("N"), ".tmp");
        await File.WriteAllTextAsync(tempPath, manifest.Json, Encoding.UTF8, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
        File.SetLastWriteTimeUtc(path, manifest.UpdatedAt.UtcDateTime);

        manifests[new ManifestRegistryKey(manifest.ProductId, manifest.ReleaseId, manifest.Environment)] = manifest;
        ManifestPersisted(logger, manifest.ProductId, manifest.ReleaseId, manifest.Environment, path, null);
    }

    private void LoadExistingManifests()
    {
        if (!Directory.Exists(manifestDirectory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(manifestDirectory, "*.manifest.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                DateTimeOffset updatedAt = File.GetLastWriteTimeUtc(path);
                DistributionManifest manifest = DistributionManifest.FromJson(json, updatedAt);
                manifests[new ManifestRegistryKey(manifest.ProductId, manifest.ReleaseId, manifest.Environment)] = manifest;
            }
            catch (JsonException ex)
            {
                ManifestSkipped(logger, path, ex);
            }
        }
    }

    private string GetManifestPath(string productId, string releaseId, string environment) =>
        Path.Combine(manifestDirectory, string.Concat(CreateStorageKey(productId, releaseId, environment), ".manifest.json"));

    private static string CreateStorageKey(string productId, string releaseId, string environment)
    {
        string key = string.Join('\n', productId, releaseId, environment);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private readonly record struct ManifestRegistryKey(
        string ProductId,
        string ReleaseId,
        string Environment);
}
