namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-7 TR-10 TR-11 v1 result from an exposure upload coordinator flush.</summary>
/// <param name="UploadedCount">Number of exposure events uploaded.</param>
/// <param name="SkippedByCadence">Indicates the flush was skipped to preserve the telemetry cadence budget.</param>
/// <param name="ErrorMessage">Optional upload failure detail.</param>
public sealed record SharpNinjaExposureUploadResult(
    int UploadedCount,
    bool SkippedByCadence = false,
    string? ErrorMessage = null);
