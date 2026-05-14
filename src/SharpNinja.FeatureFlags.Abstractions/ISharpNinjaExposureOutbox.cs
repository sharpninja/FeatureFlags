namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-5 TR-7 TR-11 v1 durable outbox for offline exposure events.</summary>
public interface ISharpNinjaExposureOutbox : ISharpNinjaExposureEventSink, ISharpNinjaExposureEventBuffer
{
    /// <summary>FR-8 dequeues a batch for best-effort upload.</summary>
    /// <param name="maximumCount">Maximum number of exposure events to dequeue.</param>
    /// <returns>Dequeued exposure events in record order.</returns>
    IReadOnlyList<SharpNinjaExposureEvent> DequeueBatch(int maximumCount);

    /// <summary>FR-8 requeues events after an upload failure.</summary>
    /// <param name="events">Exposure events to requeue.</param>
    void Requeue(IReadOnlyCollection<SharpNinjaExposureEvent> events);
}
