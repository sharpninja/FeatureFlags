namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-7 TR-10 TR-11 v1 result from an exposure upload coordinator flush.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
/// <param name="UploadedCount">Number of exposure events uploaded.</param>
/// <param name="SkippedByCadence">Indicates the flush was skipped to preserve the telemetry cadence budget.</param>
/// <param name="ErrorMessage">Optional upload failure detail.</param>
public sealed record SharpNinjaExposureUploadResult(
    int UploadedCount,
    bool SkippedByCadence = false,
    string? ErrorMessage = null);
