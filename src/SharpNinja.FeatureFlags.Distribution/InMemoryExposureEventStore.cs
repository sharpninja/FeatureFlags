using System.Collections.Concurrent;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class InMemoryExposureEventStore : IExposureEventStore
{
    private static readonly Action<ILogger, int, string, string, string, Exception?> ExposureEventsAccepted =
        LoggerMessage.Define<int, string, string, string>(
            LogLevel.Information,
            new EventId(1, nameof(ExposureEventsAccepted)),
            "Accepted {ExposureEventCount} exposure event(s) for {ProductId}/{ReleaseId}/{Environment}.");

    private readonly ConcurrentQueue<StoredExposureEvent> events = [];
    private readonly ILogger<InMemoryExposureEventStore> logger;
    private long eventCount;

    public InMemoryExposureEventStore(ILogger<InMemoryExposureEventStore> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    public long Count => Interlocked.Read(ref eventCount);

    public ValueTask<int> AppendAsync(ExposureBatchRequest batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset receivedAt = DateTimeOffset.UtcNow;
        foreach (ExposureEventRequest exposureEvent in batch.Events)
        {
            events.Enqueue(new StoredExposureEvent(
                batch.ProductId,
                batch.ReleaseId,
                batch.Environment,
                exposureEvent.FlagKey,
                exposureEvent.ResolvedValue.Clone(),
                exposureEvent.MatchedRuleIndex,
                exposureEvent.ContextFingerprint,
                exposureEvent.Timestamp,
                receivedAt));
        }

        int accepted = batch.Events.Count;
        Interlocked.Add(ref eventCount, accepted);
        ExposureEventsAccepted(
            logger,
            accepted,
            batch.ProductId,
            batch.ReleaseId,
            batch.Environment,
            null);

        return ValueTask.FromResult(accepted);
    }

    public IReadOnlyList<StoredExposureEvent> Snapshot() => events.ToArray();
}
