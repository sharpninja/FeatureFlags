namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-7 TR-9 TR-11 v1 injectable transport boundary for best-effort exposure uploads.</summary>
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
