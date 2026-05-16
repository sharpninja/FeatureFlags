namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-5 TR-7 TR-11 exposes buffered SDK exposure events for diagnostics and upload workers.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-5"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface ISharpNinjaExposureEventBuffer
{
    /// <summary>FR-8 returns a stable snapshot of buffered exposure events.</summary>
    /// <returns>Buffered exposure events in record order.</returns>
    IReadOnlyList<SharpNinjaExposureEvent> Snapshot();

    /// <summary>FR-8 clears all buffered exposure events.</summary>
    void Clear();
}
