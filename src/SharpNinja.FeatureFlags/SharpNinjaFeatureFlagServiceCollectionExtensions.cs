using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags;

/// <summary>FR-4 TR-11 Phase 1 service registration extensions for SharpNinja Feature Flags.</summary>
public static class SharpNinjaFeatureFlagServiceCollectionExtensions
{
    /// <summary>Registers SharpNinja Feature Flags services from a manifest JSON payload.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="options">SDK feature flag options.</param>
    /// <param name="manifestJson">Feature flag manifest JSON.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlags(
        this IServiceCollection services,
        SharpNinjaFeatureFlagOptions options,
        string manifestJson)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);

        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(manifestJson);

        services.AddSingleton(options);
        services.AddSingleton(manifest);
        services.AddSingleton(static provider =>
        {
            ILogger<FeatureFlagEvaluator> logger =
                provider.GetService<ILogger<FeatureFlagEvaluator>>() ?? NullLogger<FeatureFlagEvaluator>.Instance;

            return new FeatureFlagEvaluator(logger);
        });
        services.AddSingleton<ISharpNinjaFeatureClient, SharpNinjaFeatureClient>();

        return services;
    }
}
