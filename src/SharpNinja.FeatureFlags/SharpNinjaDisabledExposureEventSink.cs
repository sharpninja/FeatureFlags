using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags;

/// <summary>FR-8 TR-5 TR-7 TR-11 v1 SDK sink that disables exposure recording.</summary>
internal sealed class SharpNinjaDisabledExposureEventSink : ISharpNinjaExposureEventSink
{
    /// <inheritdoc />
    public void Record(SharpNinjaExposureEvent exposureEvent)
    {
        ArgumentNullException.ThrowIfNull(exposureEvent);
    }
}
