namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-1 FR-8 FR-11 TR-9 TR-11 v1 contract: host-configured feature flag SDK options.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-1"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
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
    TimeSpan ExposureUploadInterval)
{
    /// <summary>FR-1 FR-11 v1 default options for a local TruckMate stable build.</summary>
    public static SharpNinjaFeatureFlagOptions Default { get; } =
        new(
            SharpNinjaProductCatalog.TruckMate,
            "truckmate-0.0.0-stable-0",
            SharpNinjaDeploymentEnvironment.Development.Name,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30))
        {
            ReleaseLineage = SharpNinjaReleaseLineage.Default,
        };

    /// <summary>FR-1 v1 contract: release lineage stamped for the binary.</summary>
    public SharpNinjaReleaseLineage ReleaseLineage { get; init; } =
        SharpNinjaReleaseLineage.FromReleaseId(ReleaseId);

    /// <summary>FR-11 v1 contract: deployment environment used for manifest selection.</summary>
    public SharpNinjaDeploymentEnvironment DeploymentEnvironment { get; init; } =
        SharpNinjaDeploymentEnvironment.Create(Environment);

    /// <summary>FR-11 v1 contract: allows environments beyond development, staging, and production.</summary>
    public bool AllowCustomEnvironments { get; init; } = true;

    /// <summary>FR-8 v1 contract: user-definable exposure event retention policy.</summary>
    public SharpNinjaExposureRetentionOptions ExposureRetention { get; init; } =
        SharpNinjaExposureRetentionOptions.Default;

    /// <summary>TR-9 v1 contract: tenant deployment and tenant context behavior.</summary>
    public SharpNinjaMultiTenantOptions MultiTenant { get; init; } =
        SharpNinjaMultiTenantOptions.SingleTenant;

    /// <summary>FR-1 v1 contract: product identifiers supported by this options instance.</summary>
    public IReadOnlyCollection<string> SupportedProductIds { get; init; } =
        SharpNinjaProductCatalog.V1ProductIds;

    /// <summary>FR-3 TR-6 v1 contract: optional durable path for the last verified signed manifest envelope.</summary>
    public string? ManifestCachePath { get; init; }

    /// <summary>FR-8 TR-7 v1 contract: optional durable path for the exposure event outbox.</summary>
    public string? ExposureOutboxPath { get; init; }

    /// <summary>FR-3 TR-9 v1 contract: optional Distribution service base URI used for remote manifest fetches.</summary>
    public Uri? DistributionBaseUri { get; init; }

    /// <summary>FR-8 TR-7 TR-9 v1 contract: optional exposure upload endpoint, overriding the Distribution base URI default.</summary>
    public Uri? ExposureUploadEndpoint { get; init; }

    /// <summary>FR-8 TR-7 v1 contract: maximum exposure events uploaded in one coalesced batch.</summary>
    public int ExposureUploadBatchSize { get; init; } = 100;

    /// <summary>FR-1 FR-8 FR-11 TR-9 v1 contract: validates option invariants before runtime use.</summary>
    /// <returns>The current options instance when validation succeeds.</returns>
    /// <exception cref="ArgumentException">Thrown when a required string option is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an interval or retention value is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when option values conflict.</exception>
    public SharpNinjaFeatureFlagOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ReleaseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Environment);
        ArgumentNullException.ThrowIfNull(ReleaseLineage);
        ArgumentNullException.ThrowIfNull(DeploymentEnvironment);
        ArgumentNullException.ThrowIfNull(ExposureRetention);
        ArgumentNullException.ThrowIfNull(MultiTenant);
        ArgumentNullException.ThrowIfNull(SupportedProductIds);

        if (!ContainsProductId(SupportedProductIds, ProductId))
        {
            throw new InvalidOperationException(
                $"ProductId '{ProductId}' is not included in {nameof(SupportedProductIds)}.");
        }

        if (ManifestRefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ManifestRefreshInterval),
                ManifestRefreshInterval,
                "Manifest refresh interval must be greater than zero.");
        }

        if (ExposureUploadInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExposureUploadInterval),
                ExposureUploadInterval,
                "Exposure upload interval must be greater than zero.");
        }

        if (ExposureUploadBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExposureUploadBatchSize),
                ExposureUploadBatchSize,
                "Exposure upload batch size must be greater than zero.");
        }

        if (ManifestCachePath is not null && string.IsNullOrWhiteSpace(ManifestCachePath))
        {
            throw new ArgumentException("Manifest cache path cannot be blank when specified.", nameof(ManifestCachePath));
        }

        if (ExposureOutboxPath is not null && string.IsNullOrWhiteSpace(ExposureOutboxPath))
        {
            throw new ArgumentException("Exposure outbox path cannot be blank when specified.", nameof(ExposureOutboxPath));
        }

        if (DistributionBaseUri is not null && !DistributionBaseUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException($"{nameof(DistributionBaseUri)} must be absolute when specified.");
        }

        if (ExposureUploadEndpoint is not null && !ExposureUploadEndpoint.IsAbsoluteUri)
        {
            throw new InvalidOperationException($"{nameof(ExposureUploadEndpoint)} must be absolute when specified.");
        }

        ReleaseLineage.Validate();
        DeploymentEnvironment.Validate(AllowCustomEnvironments);
        ExposureRetention.Validate();
        MultiTenant.Validate();

        if (!string.Equals(ReleaseLineage.ReleaseId, ReleaseId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{nameof(ReleaseLineage)}.{nameof(SharpNinjaReleaseLineage.ReleaseId)} must match {nameof(ReleaseId)}.");
        }

        if (!string.Equals(DeploymentEnvironment.Name, Environment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{nameof(DeploymentEnvironment)}.{nameof(SharpNinjaDeploymentEnvironment.Name)} must match {nameof(Environment)}.");
        }

        return this;
    }

    private static bool ContainsProductId(IEnumerable<string> productIds, string productId)
    {
        foreach (string supportedProductId in productIds)
        {
            if (string.Equals(supportedProductId, productId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
