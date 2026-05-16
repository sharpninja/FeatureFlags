using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 FR-8 TR-9 TR-10 TR-11 v1 DI registration for the Distribution service runtime.</summary>
/// <remarks>
/// Registration is idempotent for the Distribution services it owns; consumer registrations are preserved.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
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
        return AddSharpNinjaFeatureFlagDistributionCore(services, configuration: null, configure);
    }

    /// <summary>Registers Distribution runtime services from configuration plus optional code-based overrides.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configuration">Distribution configuration section.</param>
    /// <param name="configure">Optional Distribution runtime configuration callback.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlagDistribution(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<SharpNinjaDistributionBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return AddSharpNinjaFeatureFlagDistributionCore(services, configuration, configure);
    }

    private static IServiceCollection AddSharpNinjaFeatureFlagDistributionCore(
        IServiceCollection services,
        IConfiguration? configuration,
        Action<SharpNinjaDistributionBuilder>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new SharpNinjaDistributionBuilder();
        if (configuration is not null)
        {
            ApplyConfiguration(builder, configuration);
        }

        configure?.Invoke(builder);
        SharpNinjaDistributionOptions options = builder.BuildOptions();

        services.AddSingleton<IOptions<SharpNinjaDistributionOptions>>(Options.Create(options));
        if (options.StorageMode == SharpNinjaDistributionStorageMode.FileSystem)
        {
            services.TryAddSingleton<IDistributionManifestRegistry, FileBackedDistributionManifestRegistry>();
            services.TryAddSingleton<IExposureEventStore, FileBackedExposureEventStore>();
        }
        else
        {
            services.TryAddSingleton<IDistributionManifestRegistry, InMemoryDistributionManifestRegistry>();
            services.TryAddSingleton<IExposureEventStore, InMemoryExposureEventStore>();
        }

        services.TryAddSingleton<IProductApiKeyValidator, OptionsProductApiKeyValidator>();
        services.TryAddSingleton<IDeviceAttestationPolicy, OptionsDeviceAttestationPolicy>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDeviceAttestationValidator, ConfiguredDeviceAttestationValidator>());
        services.TryAddSingleton<DistributionMetrics>();
        services.TryAddSingleton<DistributionRequestAuthorizer>();
        services.TryAddSingleton<DistributionEndpointHandler>();

        return services;
    }

    private static void ApplyConfiguration(SharpNinjaDistributionBuilder builder, IConfiguration configuration)
    {
        builder.DefaultEnvironment = ReadString(configuration["DefaultEnvironment"], builder.DefaultEnvironment);
        builder.RequireDeviceAttestation = ReadBoolean(
            configuration["Authorization:RequireDeviceAttestation"]
            ?? configuration["DeviceAttestation:RequireDeviceAttestation"],
            builder.RequireDeviceAttestation);
        builder.StorageMode = ReadEnum(configuration["Storage:Mode"], builder.StorageMode);
        builder.StorageRootPath = ReadString(configuration["Storage:RootPath"], builder.StorageRootPath);
        builder.EnableCdnCacheHeaders = ReadBoolean(configuration["Cdn:EnableCacheHeaders"], builder.EnableCdnCacheHeaders);
        builder.ManifestMaxAge = ReadSeconds(configuration["Cdn:ManifestMaxAgeSeconds"], builder.ManifestMaxAge);
        builder.ManifestStaleWhileRevalidate = ReadSeconds(
            configuration["Cdn:ManifestStaleWhileRevalidateSeconds"],
            builder.ManifestStaleWhileRevalidate);
        builder.ManifestStaleIfError = ReadSeconds(configuration["Cdn:ManifestStaleIfErrorSeconds"], builder.ManifestStaleIfError);

        AddKeyMap(builder.ProductApiKeys, configuration.GetSection("ApiKeys"));
        AddKeyMap(builder.ProductApiKeys, configuration.GetSection("ProductApiKeys"));
        AddKeyMap(builder.DeviceAttestationTestTokens, configuration.GetSection("DeviceAttestation:TestTokens"));
    }

    private static void AddKeyMap(Dictionary<string, List<string>> target, IConfiguration section)
    {
        foreach (IConfigurationSection productSection in section.GetChildren())
        {
            List<string> values = productSection
                .GetChildren()
                .Select(static child => child.Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .ToList();

            if (values.Count == 0 && !string.IsNullOrWhiteSpace(productSection.Value))
            {
                values.Add(productSection.Value);
            }

            if (values.Count > 0)
            {
                target[productSection.Key] = values;
            }
        }
    }

    private static string ReadString(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static bool ReadBoolean(string? value, bool fallback) =>
        bool.TryParse(value, out bool result) ? result : fallback;

    private static TimeSpan ReadSeconds(string? value, TimeSpan fallback) =>
        int.TryParse(value, out int seconds) && seconds >= 0 ? TimeSpan.FromSeconds(seconds) : fallback;

    private static SharpNinjaDistributionStorageMode ReadEnum(string? value, SharpNinjaDistributionStorageMode fallback) =>
        Enum.TryParse(value, ignoreCase: true, out SharpNinjaDistributionStorageMode result) ? result : fallback;
}
