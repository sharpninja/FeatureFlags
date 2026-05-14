namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-5 TR-7 TR-11 exposes buffered SDK exposure events for diagnostics and upload workers.</summary>
public interface ISharpNinjaExposureEventBuffer
{
    /// <summary>FR-8 returns a stable snapshot of buffered exposure events.</summary>
    /// <returns>Buffered exposure events in record order.</returns>
    IReadOnlyList<SharpNinjaExposureEvent> Snapshot();

    /// <summary>FR-8 clears all buffered exposure events.</summary>
    void Clear();
}
