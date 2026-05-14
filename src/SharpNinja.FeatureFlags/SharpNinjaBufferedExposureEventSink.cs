using System.Collections.ObjectModel;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags;

/// <summary>FR-8 TR-5 TR-7 TR-11 v1 SDK sink that buffers exposure events in memory.</summary>
internal sealed class SharpNinjaBufferedExposureEventSink : ISharpNinjaExposureEventSink, ISharpNinjaExposureEventBuffer
{
    private readonly Lock gate = new();
    private readonly List<SharpNinjaExposureEvent> events = [];

    /// <inheritdoc />
    public void Record(SharpNinjaExposureEvent exposureEvent)
    {
        ArgumentNullException.ThrowIfNull(exposureEvent);

        lock (gate)
        {
            events.Add(exposureEvent);
        }
    }

    /// <summary>FR-8 returns a stable snapshot of buffered exposure events.</summary>
    /// <returns>Buffered exposure events in record order.</returns>
    public IReadOnlyList<SharpNinjaExposureEvent> Snapshot()
    {
        lock (gate)
        {
            return new ReadOnlyCollection<SharpNinjaExposureEvent>([.. events]);
        }
    }

    /// <summary>FR-8 clears all buffered exposure events.</summary>
    public void Clear()
    {
        lock (gate)
        {
            events.Clear();
        }
    }
}
