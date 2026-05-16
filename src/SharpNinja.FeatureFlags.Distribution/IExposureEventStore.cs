namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-8 TR-7 TR-9 TR-10 TR-11 v1 provider-ready exposure event store abstraction.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface IExposureEventStore
{
    /// <summary>TR-10 gets the number of exposure events accepted by the store.</summary>
    long Count { get; }

    /// <summary>FR-8 appends one authenticated exposure batch to the store.</summary>
    /// <param name="batch">Exposure batch to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of exposure events accepted.</returns>
    ValueTask<int> AppendAsync(ExposureBatchRequest batch, CancellationToken cancellationToken);

    /// <summary>FR-8 returns a stable in-process snapshot for diagnostics and focused tests.</summary>
    /// <returns>Exposure events currently visible to the store.</returns>
    IReadOnlyList<StoredExposureEvent> Snapshot();
}
