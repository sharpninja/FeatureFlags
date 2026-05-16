using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-3 FR-7 TR-6 TR-9 TR-11 v1 injectable transport boundary for fetching signed remote manifests.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaRemoteManifestClient
{
    /// <summary>FR-3 fetches a signed manifest envelope from the configured Distribution source.</summary>
    /// <param name="options">SDK options.</param>
    /// <param name="forceRefresh">Whether this refresh bypasses normal cache cadence for kill-switch scenarios.</param>
    /// <param name="currentETag">Current manifest ETag for conditional requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Remote manifest fetch result.</returns>
    ValueTask<RemoteManifestFetchResult> FetchAsync(
        SharpNinjaFeatureFlagOptions options,
        bool forceRefresh,
        string? currentETag,
        CancellationToken cancellationToken = default);
}
