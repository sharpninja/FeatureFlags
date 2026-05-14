namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-3 FR-8 Phase 0 contract: host-configured feature flag SDK options.</summary>
/// <param name="ProductId">Compile-time product identifier.</param>
/// <param name="ReleaseId">Compile-time release identifier.</param>
/// <param name="Environment">Target environment.</param>
/// <param name="ManifestRefreshInterval">Normal manifest refresh interval.</param>
/// <param name="ExposureUploadInterval">Exposure telemetry upload interval.</param>
public sealed record SharpNinjaFeatureFlagOptions(
    string ProductId,
    string ReleaseId,
    string Environment,
    TimeSpan ManifestRefreshInterval,
    TimeSpan ExposureUploadInterval);
