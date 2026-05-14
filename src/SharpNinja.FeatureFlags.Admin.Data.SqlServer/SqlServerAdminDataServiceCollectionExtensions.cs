using Microsoft.Extensions.DependencyInjection;

namespace SharpNinja.FeatureFlags.Admin.Data.SqlServer;

/// <summary>FR-9 FR-11 TR-11: DI registration extensions for the SQL Server admin data provider.</summary>
public static class SqlServerAdminDataServiceCollectionExtensions
{
    /// <summary>Registers SQL Server admin data-provider metadata and options.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="options">Optional provider options supplied by the admin host.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlagsAdminDataSqlServer(
        this IServiceCollection services,
        AdminDataProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        AdminDataProviderDescriptor descriptor = SqlServerAdminDataProvider.Descriptor;
        AdminDataProviderOptions normalizedOptions = NormalizeOptions(options, descriptor);
        var registration = new AdminDataProviderRegistration(descriptor, normalizedOptions);

        services.AddSingleton(descriptor);
        services.AddSingleton(normalizedOptions);
        services.AddSingleton(registration);

        return services;
    }

    private static AdminDataProviderOptions NormalizeOptions(
        AdminDataProviderOptions? options,
        AdminDataProviderDescriptor descriptor)
    {
        AdminDataProviderOptions configured = options ?? new AdminDataProviderOptions();

        return configured with
        {
            DefaultSchema = string.IsNullOrWhiteSpace(configured.DefaultSchema)
                ? descriptor.DefaultSchema
                : configured.DefaultSchema,
            MigrationsHistoryTableName = string.IsNullOrWhiteSpace(configured.MigrationsHistoryTableName)
                ? descriptor.MigrationsHistoryTableName
                : configured.MigrationsHistoryTableName,
        };
    }
}
