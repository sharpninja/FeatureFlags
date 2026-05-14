using Microsoft.Extensions.Logging.Abstractions;

namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9 TR-10 TR-11: DI registration extensions for the Admin runtime.</summary>
public static class AdminRuntimeServiceCollectionExtensions
{
    /// <summary>Registers the in-memory Admin runtime foundation and immutable audit service.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlagsAdminRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAdminRuntimeService>(static provider =>
        {
            ILogger<InMemoryAdminRuntimeService> logger =
                provider.GetService<ILogger<InMemoryAdminRuntimeService>>()
                ?? NullLogger<InMemoryAdminRuntimeService>.Instance;

            return new InMemoryAdminRuntimeService(logger);
        });

        return services;
    }
}
