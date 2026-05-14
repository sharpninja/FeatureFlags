namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 builder for Distribution service registration.</summary>
public sealed class SharpNinjaDistributionBuilder
{
    internal SharpNinjaDistributionBuilder()
    {
    }

    /// <summary>FR-6 v1 default manifest environment used when callers omit the environment query value.</summary>
    public string DefaultEnvironment { get; set; } = "Development";

    /// <summary>TR-9 v1 in-memory product API key map keyed by ProductId.</summary>
    public Dictionary<string, List<string>> ProductApiKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>TR-9 v1 deterministic test attestation token map keyed by ProductId or <c>*</c>.</summary>
    public Dictionary<string, List<string>> DeviceAttestationTestTokens { get; } = new(StringComparer.Ordinal);

    /// <summary>TR-9 v1 requires a successful device-attestation validator for Distribution requests.</summary>
    public bool RequireDeviceAttestation { get; set; }

    /// <summary>FR-3 FR-8 v1 built-in manifest and exposure storage mode.</summary>
    public SharpNinjaDistributionStorageMode StorageMode { get; set; }

    /// <summary>FR-3 FR-8 v1 durable file-system root used when <see cref="StorageMode"/> is <see cref="SharpNinjaDistributionStorageMode.FileSystem"/>.</summary>
    public string StorageRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "distribution");

    /// <summary>FR-3 TR-10 v1 enables CDN-friendly manifest response headers.</summary>
    public bool EnableCdnCacheHeaders { get; set; } = true;

    /// <summary>FR-3 v1 CDN manifest max-age value.</summary>
    public TimeSpan ManifestMaxAge { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>FR-3 v1 CDN stale-while-revalidate value.</summary>
    public TimeSpan ManifestStaleWhileRevalidate { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>FR-3 v1 CDN stale-if-error value.</summary>
    public TimeSpan ManifestStaleIfError { get; set; } = TimeSpan.FromHours(1);

    internal SharpNinjaDistributionOptions BuildOptions() =>
        new(
            DefaultEnvironment,
            ProductApiKeys,
            DeviceAttestationTestTokens,
            RequireDeviceAttestation,
            StorageMode,
            StorageRootPath,
            EnableCdnCacheHeaders,
            ManifestMaxAge,
            ManifestStaleWhileRevalidate,
            ManifestStaleIfError);
}
