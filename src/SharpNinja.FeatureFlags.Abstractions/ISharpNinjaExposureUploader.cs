namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-7 TR-9 TR-11 v1 injectable transport boundary for best-effort exposure uploads.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaExposureUploader
{
    /// <summary>FR-8 uploads a coalesced exposure event batch.</summary>
    /// <param name="events">Exposure events to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the upload.</returns>
    ValueTask UploadAsync(
        IReadOnlyCollection<SharpNinjaExposureEvent> events,
        CancellationToken cancellationToken = default);
}
