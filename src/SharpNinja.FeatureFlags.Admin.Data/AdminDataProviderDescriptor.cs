namespace SharpNinja.FeatureFlags.Admin.Data;

/// <summary>FR-9 FR-11 TR-11: Describes a supported SharpNinja admin-plane data provider.</summary>
/// <param name="ProviderName">The provider name used by admin-plane configuration.</param>
/// <param name="DisplayName">The human-readable provider name.</param>
/// <param name="MigrationsAssemblyName">The assembly that will contain provider-specific migrations.</param>
/// <param name="MigrationsHistoryTableName">The common migrations history table name.</param>
/// <param name="DefaultSchema">The provider's default database schema.</param>
/// <param name="SupportsMultiTenant">Whether the provider is available for multi-tenant v1 deployments.</param>
/// <param name="SupportsUserDefinedExposureRetention">
/// Whether the provider is available for user-definable exposure retention.
/// </param>
public sealed record AdminDataProviderDescriptor(
    string ProviderName,
    string DisplayName,
    string MigrationsAssemblyName,
    string MigrationsHistoryTableName,
    string DefaultSchema,
    bool SupportsMultiTenant,
    bool SupportsUserDefinedExposureRetention);
