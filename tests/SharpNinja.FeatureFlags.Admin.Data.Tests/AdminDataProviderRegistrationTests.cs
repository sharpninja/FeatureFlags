using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Admin.Data;
using SharpNinja.FeatureFlags.Admin.Data.Postgres;
using SharpNinja.FeatureFlags.Admin.Data.SqlServer;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.Data.Tests;

/// <summary>FR-9 FR-11 TR-11: Admin data-provider registration tests for PostgreSQL and SQL Server.</summary>
public sealed class AdminDataProviderRegistrationTests
{
    /// <summary>PostgreSQL registration exposes the expected descriptor and normalized options.</summary>
    [Fact]
    public void PostgresProviderRegistersExpectedMetadataAndOptions()
    {
        var options = new AdminDataProviderOptions(ConnectionString: "Host=localhost;Database=flags");

        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlagsAdminDataPostgres(options)
            .BuildServiceProvider();

        AdminDataProviderRegistration registration = provider.GetRequiredService<AdminDataProviderRegistration>();

        Assert.Equal("Postgres", registration.Descriptor.ProviderName);
        Assert.Equal("PostgreSQL", registration.Descriptor.DisplayName);
        Assert.Equal("SharpNinja.FeatureFlags.Admin.Data.Postgres", registration.Descriptor.MigrationsAssemblyName);
        Assert.Equal("__SharpNinjaMigrations", registration.Descriptor.MigrationsHistoryTableName);
        Assert.Equal("public", registration.Descriptor.DefaultSchema);
        Assert.True(registration.Descriptor.SupportsMultiTenant);
        Assert.True(registration.Descriptor.SupportsUserDefinedExposureRetention);
        Assert.Equal("Host=localhost;Database=flags", registration.Options.ConnectionString);
        Assert.Equal("public", registration.Options.DefaultSchema);
        Assert.Equal("__SharpNinjaMigrations", registration.Options.MigrationsHistoryTableName);
        Assert.True(registration.Options.MultiTenantEnabled);
        Assert.True(registration.Options.UserDefinedExposureRetentionEnabled);
    }

    /// <summary>SQL Server registration exposes the expected descriptor and normalized options.</summary>
    [Fact]
    public void SqlServerProviderRegistersExpectedMetadataAndOptions()
    {
        var options = new AdminDataProviderOptions(ConnectionString: "Server=localhost;Database=flags");

        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlagsAdminDataSqlServer(options)
            .BuildServiceProvider();

        AdminDataProviderRegistration registration = provider.GetRequiredService<AdminDataProviderRegistration>();

        Assert.Equal("SqlServer", registration.Descriptor.ProviderName);
        Assert.Equal("SQL Server", registration.Descriptor.DisplayName);
        Assert.Equal("SharpNinja.FeatureFlags.Admin.Data.SqlServer", registration.Descriptor.MigrationsAssemblyName);
        Assert.Equal("__SharpNinjaMigrations", registration.Descriptor.MigrationsHistoryTableName);
        Assert.Equal("dbo", registration.Descriptor.DefaultSchema);
        Assert.True(registration.Descriptor.SupportsMultiTenant);
        Assert.True(registration.Descriptor.SupportsUserDefinedExposureRetention);
        Assert.Equal("Server=localhost;Database=flags", registration.Options.ConnectionString);
        Assert.Equal("dbo", registration.Options.DefaultSchema);
        Assert.Equal("__SharpNinjaMigrations", registration.Options.MigrationsHistoryTableName);
        Assert.True(registration.Options.MultiTenantEnabled);
        Assert.True(registration.Options.UserDefinedExposureRetentionEnabled);
    }

    /// <summary>Both v1 provider registrations can be discovered from one service collection.</summary>
    [Fact]
    public void BothProviderRegistrationsCanBeDiscoveredTogether()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddSharpNinjaFeatureFlagsAdminDataPostgres()
            .AddSharpNinjaFeatureFlagsAdminDataSqlServer()
            .BuildServiceProvider();

        string[] providerNames = provider
            .GetServices<AdminDataProviderRegistration>()
            .Select(registration => registration.Descriptor.ProviderName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Postgres", "SqlServer"], providerNames);
    }
}
