using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 DI registration for the Distribution service runtime.</summary>
public static class SharpNinjaDistributionServiceCollectionExtensions
{
    /// <summary>Registers in-memory Distribution runtime services for manifest and exposure endpoints.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">Optional Distribution runtime configuration callback.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlagDistribution(
        this IServiceCollection services,
        Action<SharpNinjaDistributionBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new SharpNinjaDistributionBuilder();
        configure?.Invoke(builder);

        services.AddSingleton<IOptions<SharpNinjaDistributionOptions>>(Options.Create(builder.BuildOptions()));
        services.AddSingleton<IDistributionManifestRegistry, InMemoryDistributionManifestRegistry>();
        services.AddSingleton<IProductApiKeyValidator, OptionsProductApiKeyValidator>();
        services.AddSingleton<IExposureEventStore, InMemoryExposureEventStore>();
        services.AddSingleton<DistributionEndpointHandler>();

        return services;
    }
}
