namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-5 TR-7 TR-11 v1 contract: synchronous sink for SDK exposure events.</summary>
public interface ISharpNinjaExposureEventSink
{
    /// <summary>FR-8 records an exposure event without blocking evaluation on network state.</summary>
    /// <param name="exposureEvent">Exposure event to record.</param>
    void Record(SharpNinjaExposureEvent exposureEvent);
}
