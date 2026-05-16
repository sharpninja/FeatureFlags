namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-8 TR-5 TR-7 TR-11 v1 durable outbox for offline exposure events.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-5"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
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
