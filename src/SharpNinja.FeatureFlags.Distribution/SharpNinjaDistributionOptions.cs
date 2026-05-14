namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 options for the Distribution service runtime.</summary>
internal sealed class SharpNinjaDistributionOptions
{
    public SharpNinjaDistributionOptions(
        string defaultEnvironment,
        IReadOnlyDictionary<string, List<string>> productApiKeys,
        IReadOnlyDictionary<string, List<string>> deviceAttestationTestTokens,
        bool requireDeviceAttestation,
        SharpNinjaDistributionStorageMode storageMode,
        string storageRootPath,
        bool enableCdnCacheHeaders,
        TimeSpan manifestMaxAge,
        TimeSpan manifestStaleWhileRevalidate,
        TimeSpan manifestStaleIfError)
    {
        ArgumentNullException.ThrowIfNull(productApiKeys);
        ArgumentNullException.ThrowIfNull(deviceAttestationTestTokens);

        DefaultEnvironment = string.IsNullOrWhiteSpace(defaultEnvironment)
            ? "Development"
            : defaultEnvironment;
        ProductApiKeys = productApiKeys.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.Ordinal);
        DeviceAttestationTestTokens = deviceAttestationTestTokens.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.Ordinal);
        RequireDeviceAttestation = requireDeviceAttestation;
        StorageMode = storageMode;
        StorageRootPath = string.IsNullOrWhiteSpace(storageRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "distribution")
            : storageRootPath;
        EnableCdnCacheHeaders = enableCdnCacheHeaders;
        ManifestMaxAge = NormalizeNonNegative(manifestMaxAge);
        ManifestStaleWhileRevalidate = NormalizeNonNegative(manifestStaleWhileRevalidate);
        ManifestStaleIfError = NormalizeNonNegative(manifestStaleIfError);
    }

    public string DefaultEnvironment { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ProductApiKeys { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DeviceAttestationTestTokens { get; }

    public bool RequireDeviceAttestation { get; }

    public SharpNinjaDistributionStorageMode StorageMode { get; }

    public string StorageRootPath { get; }

    public bool EnableCdnCacheHeaders { get; }

    public TimeSpan ManifestMaxAge { get; }

    public TimeSpan ManifestStaleWhileRevalidate { get; }

    public TimeSpan ManifestStaleIfError { get; }

    private static TimeSpan NormalizeNonNegative(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
