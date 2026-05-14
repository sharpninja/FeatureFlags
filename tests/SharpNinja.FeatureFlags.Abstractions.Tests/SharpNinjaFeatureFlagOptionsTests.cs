using SharpNinja.FeatureFlags.Abstractions.Options;
using Xunit;

namespace SharpNinja.FeatureFlags.Abstractions.Tests;

/// <summary>FR-1 FR-8 FR-11 TR-9 v1 contract tests for feature flag options.</summary>
public sealed class SharpNinjaFeatureFlagOptionsTests
{
    /// <summary>Default options expose the v1 product, environment, exposure, and tenant defaults.</summary>
    [Fact]
    public void DefaultOptionsValidateAndExposeV1Defaults()
    {
        SharpNinjaFeatureFlagOptions options = SharpNinjaFeatureFlagOptions.Default.Validate();

        Assert.Equal(SharpNinjaProductCatalog.TruckMate, options.ProductId);
        Assert.Contains(SharpNinjaProductCatalog.TruckMate, options.SupportedProductIds);
        Assert.Contains(SharpNinjaProductCatalog.DriverMate, options.SupportedProductIds);
        Assert.True(options.AllowCustomEnvironments);
        Assert.Equal(SharpNinjaDeploymentEnvironment.Development, options.DeploymentEnvironment);
        Assert.Equal(TimeSpan.FromDays(90), options.ExposureRetention.RetentionPeriod);
        Assert.False(options.MultiTenant.Enabled);
        Assert.Equal(SharpNinjaMultiTenantOptions.DefaultTenantContextKey, options.MultiTenant.TenantContextKey);
    }

    /// <summary>Options validate the decided v1 multi-tenant and custom-environment surface.</summary>
    [Fact]
    public void ValidateAcceptsCustomEnvironmentAndMultiTenantOptions()
    {
        SharpNinjaFeatureFlagOptions options = CreateValidOptions();

        SharpNinjaFeatureFlagOptions validated = options.Validate();

        Assert.Same(options, validated);
        Assert.True(validated.MultiTenant.Enabled);
        Assert.Equal("TenantId", validated.MultiTenant.TenantContextKey);
        Assert.Equal("custom-prod-eu", validated.Environment);
    }

    /// <summary>Options reject product identifiers outside the decided v1 product catalog.</summary>
    [Fact]
    public void ValidateRejectsUnknownProduct()
    {
        SharpNinjaFeatureFlagOptions options = CreateValidOptions() with
        {
            ProductId = "unknown",
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    /// <summary>Options reject custom environments when the host disables custom environment names.</summary>
    [Fact]
    public void ValidateRejectsCustomEnvironmentWhenDisabled()
    {
        SharpNinjaFeatureFlagOptions options = CreateValidOptions() with
        {
            AllowCustomEnvironments = false,
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    /// <summary>Release lineage validation requires strict semantic version core values.</summary>
    [Fact]
    public void ReleaseLineageRejectsInvalidSemanticVersion()
    {
        SharpNinjaReleaseLineage lineage = new(
            ReleaseId: "truckmate-bad-release",
            SemanticVersion: "1.2",
            Channel: SharpNinjaReleaseChannel.Stable,
            Build: "7");

        Assert.Throws<InvalidOperationException>(() => lineage.Validate());
    }

    /// <summary>Exposure retention validation rejects zero or negative finite retention values.</summary>
    [Fact]
    public void ExposureRetentionRejectsZeroRetentionPeriod()
    {
        SharpNinjaExposureRetentionOptions retention = new(TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => retention.Validate());
    }

    /// <summary>Tenant options require a non-empty tenant context key.</summary>
    [Fact]
    public void MultiTenantOptionsRejectBlankContextKey()
    {
        SharpNinjaMultiTenantOptions tenantOptions = new(
            Enabled: true,
            TenantContextKey: " ");

        Assert.Throws<ArgumentException>(() => tenantOptions.Validate());
    }

    private static SharpNinjaFeatureFlagOptions CreateValidOptions()
    {
        const string releaseId = "truckmate-1.2.3-beta-456";
        const string environment = "custom-prod-eu";

        return new(
            ProductId: SharpNinjaProductCatalog.TruckMate,
            ReleaseId: releaseId,
            Environment: environment,
            ManifestRefreshInterval: TimeSpan.FromMinutes(5),
            ExposureUploadInterval: TimeSpan.FromMinutes(1))
        {
            ReleaseLineage = new(
                ReleaseId: releaseId,
                SemanticVersion: "1.2.3",
                Channel: SharpNinjaReleaseChannel.Beta,
                Build: "456"),
            DeploymentEnvironment = SharpNinjaDeploymentEnvironment.Create(environment),
            ExposureRetention = new(TimeSpan.FromDays(30)),
            MultiTenant = SharpNinjaMultiTenantOptions.MultiTenant,
        };
    }
}
