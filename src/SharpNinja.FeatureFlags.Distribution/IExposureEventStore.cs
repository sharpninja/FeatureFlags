namespace SharpNinja.FeatureFlags.Distribution;

internal interface IExposureEventStore
{
    long Count { get; }

    ValueTask<int> AppendAsync(ExposureBatchRequest batch, CancellationToken cancellationToken);

    IReadOnlyList<StoredExposureEvent> Snapshot();
}
