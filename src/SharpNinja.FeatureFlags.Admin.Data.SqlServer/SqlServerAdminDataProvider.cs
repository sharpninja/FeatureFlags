namespace SharpNinja.FeatureFlags.Admin.Data.SqlServer;

/// <summary>FR-9 FR-11 TR-11: SQL Server admin data-provider metadata for v1 deployments.</summary>
public static class SqlServerAdminDataProvider
{
    /// <summary>The SQL Server provider descriptor exposed to the admin-plane composition root.</summary>
    public static AdminDataProviderDescriptor Descriptor { get; } = new(
        ProviderName: "SqlServer",
        DisplayName: "SQL Server",
        MigrationsAssemblyName: "SharpNinja.FeatureFlags.Admin.Data.SqlServer",
        MigrationsHistoryTableName: "__SharpNinjaMigrations",
        DefaultSchema: "dbo",
        SupportsMultiTenant: true,
        SupportsUserDefinedExposureRetention: true);
}
