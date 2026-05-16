using Microsoft.Extensions.DependencyInjection;

namespace SharpNinja.FeatureFlags.Admin.Data.Postgres;

/// <summary>FR-9 FR-11 TR-11: DI registration extensions for the PostgreSQL admin data provider.</summary>
/// <remarks>
/// Registration is idempotent for the Postgres provider; safe to call multiple times.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public static class PostgresAdminDataServiceCollectionExtensions
{
    /// <summary>Registers PostgreSQL admin data-provider metadata and options.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="options">Optional provider options supplied by the admin host.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlagsAdminDataPostgres(
        this IServiceCollection services,
        AdminDataProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        AdminDataProviderDescriptor descriptor = PostgresAdminDataProvider.Descriptor;
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
