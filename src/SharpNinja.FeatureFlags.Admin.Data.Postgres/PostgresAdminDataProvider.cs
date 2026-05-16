namespace SharpNinja.FeatureFlags.Admin.Data.Postgres;

/// <summary>FR-9 FR-11 TR-11: PostgreSQL admin data-provider metadata for v1 deployments.</summary>
/// <remarks>
/// Stateless provider descriptor. Safe to register as a singleton.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
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
