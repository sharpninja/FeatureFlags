namespace SharpNinja.FeatureFlags.Admin.Data;

/// <summary>FR-9 FR-11 TR-11: Captures admin data-provider registration options.</summary>
/// <param name="ConnectionString">The provider connection string supplied by the admin host.</param>
/// <param name="DefaultSchema">The schema selected for the provider registration.</param>
/// <param name="MigrationsHistoryTableName">The migrations history table selected for the provider registration.</param>
/// <param name="MultiTenantEnabled">Whether multi-tenant storage semantics are enabled for the admin host.</param>
/// <param name="UserDefinedExposureRetentionEnabled">
/// Whether user-definable exposure retention storage is enabled for the admin host.
/// </param>
public sealed record AdminDataProviderOptions(
    string ConnectionString = "",
    string DefaultSchema = "",
    string MigrationsHistoryTableName = "__SharpNinjaMigrations",
    bool MultiTenantEnabled = true,
    bool UserDefinedExposureRetentionEnabled = true);
