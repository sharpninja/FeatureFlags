namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-7 TR-10 TR-11 v1 coalesces durable exposure uploads under the configured telemetry budget.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaExposureUploadCoordinator
{
    /// <summary>FR-8 TR-7 TR-10 gets the timestamp for the most recent successful exposure upload.</summary>
    DateTimeOffset? LastSuccessfulUpload { get; }

    /// <summary>TR-7 flushes queued exposure events when cadence allows, or immediately when forced.</summary>
    /// <param name="force">Whether to bypass the cadence budget.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exposure upload result.</returns>
    ValueTask<SharpNinjaExposureUploadResult> FlushAsync(
        bool force = false,
        CancellationToken cancellationToken = default);
}
