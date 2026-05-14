namespace SharpNinja.FeatureFlags.Admin.Data.Postgres;

/// <summary>FR-9 FR-11 TR-11: PostgreSQL admin data-provider metadata for v1 deployments.</summary>
public static class PostgresAdminDataProvider
{
    /// <summary>The PostgreSQL provider descriptor exposed to the admin-plane composition root.</summary>
    public static AdminDataProviderDescriptor Descriptor { get; } = new(
        ProviderName: "Postgres",
        DisplayName: "PostgreSQL",
        MigrationsAssemblyName: "SharpNinja.FeatureFlags.Admin.Data.Postgres",
        MigrationsHistoryTableName: "__SharpNinjaMigrations",
        DefaultSchema: "public",
        SupportsMultiTenant: true,
        SupportsUserDefinedExposureRetention: true);
}
