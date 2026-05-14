using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags;

/// <summary>FR-1 FR-4 FR-8 TR-5 TR-7 TR-11 Phase 1 service registration extensions for SharpNinja Feature Flags.</summary>
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

        return services.AddSharpNinjaFeatureFlags(
            options,
            new SignedManifestEnvelope(
                manifestJson,
                "bundled-development-signature",
                "bundled-development-key",
                "structural"));
    }

    /// <summary>Registers SharpNinja Feature Flags services from a signed manifest envelope.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="options">SDK feature flag options.</param>
    /// <param name="manifestEnvelope">Signed feature flag manifest envelope.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlags(
        this IServiceCollection services,
        SharpNinjaFeatureFlagOptions options,
        SignedManifestEnvelope manifestEnvelope)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(manifestEnvelope);

        options.Validate();
        manifestEnvelope.Validate();

        services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(options);
        services.TryAddSingleton<ISharpNinjaBundledManifestProvider>(
            new SharpNinjaBundledManifestProvider(manifestEnvelope));
        services.TryAddSingleton<ISharpNinjaManifestSignatureVerifier, SharpNinjaStructuralManifestSignatureVerifier>();
        services.TryAddSingleton<ISharpNinjaManifestCacheStore, SharpNinjaDiskManifestCacheStore>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<HttpClient>();
        services.TryAddSingleton<ISharpNinjaActiveManifestStore, SharpNinjaActiveManifestStore>();
        services.TryAddSingleton(static provider => provider.GetRequiredService<ISharpNinjaActiveManifestStore>().CurrentManifest);
        services.TryAddSingleton<ISharpNinjaRemoteManifestClient, SharpNinjaHttpRemoteManifestClient>();
        services.TryAddSingleton<ISharpNinjaRemoteFetchCoordinator, SharpNinjaRemoteFetchCoordinator>();
        services.TryAddSingleton<ISharpNinjaExposureOutbox, SharpNinjaFileExposureOutbox>();
        services.TryAddSingleton<ISharpNinjaExposureEventSink>(
            static provider => provider.GetRequiredService<ISharpNinjaExposureOutbox>());
        services.TryAddSingleton<ISharpNinjaExposureEventBuffer>(
            static provider => provider.GetRequiredService<ISharpNinjaExposureOutbox>());
        services.TryAddSingleton<ISharpNinjaExposureUploader, SharpNinjaHttpExposureUploader>();
        services.TryAddSingleton<ISharpNinjaExposureUploadCoordinator, SharpNinjaExposureUploadCoordinator>();
        services.TryAddSingleton(static provider =>
        {
            ILogger<FeatureFlagEvaluator> logger =
                provider.GetService<ILogger<FeatureFlagEvaluator>>() ?? NullLogger<FeatureFlagEvaluator>.Instance;

            return new FeatureFlagEvaluator(logger);
        });
        services.TryAddSingleton<ISharpNinjaFeatureClient>(static provider => new SharpNinjaFeatureClient(
            provider.GetRequiredService<FeatureFlagEvaluator>(),
            provider.GetRequiredService<ISharpNinjaActiveManifestStore>(),
            provider.GetRequiredService<SharpNinjaFeatureFlagOptions>(),
            provider.GetRequiredService<ISharpNinjaExposureEventSink>(),
            provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<ISharpNinjaFeatureFlagAdmin, SharpNinjaFeatureFlagAdmin>();

        return services;
    }
}
