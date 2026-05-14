using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class DistributionMetrics
{
    private long authSuccessTotal;
    private long authFailureTotal;
    private long attestationSuccessTotal;
    private long attestationFailureTotal;
    private long attestationSkippedTotal;
    private long manifestCacheHitsTotal;
    private long manifestCacheMissesTotal;
    private long manifestNotModifiedTotal;
    private long exposureBatchesTotal;
    private long exposureEventsTotal;

    public void RecordAuthSuccess() => Interlocked.Increment(ref authSuccessTotal);

    public void RecordAuthFailure() => Interlocked.Increment(ref authFailureTotal);

    public void RecordAttestationSuccess() => Interlocked.Increment(ref attestationSuccessTotal);

    public void RecordAttestationFailure() => Interlocked.Increment(ref attestationFailureTotal);

    public void RecordAttestationSkipped() => Interlocked.Increment(ref attestationSkippedTotal);

    public void RecordManifestCacheHit() => Interlocked.Increment(ref manifestCacheHitsTotal);

    public void RecordManifestCacheMiss() => Interlocked.Increment(ref manifestCacheMissesTotal);

    public void RecordManifestNotModified() => Interlocked.Increment(ref manifestNotModifiedTotal);

    public void RecordExposureBatch(int eventCount)
    {
        Interlocked.Increment(ref exposureBatchesTotal);
        Interlocked.Add(ref exposureEventsTotal, eventCount);
    }

    public string RenderPrometheus(
        IDistributionManifestRegistry manifestRegistry,
        IExposureEventStore exposureEventStore,
        IOptions<SharpNinjaDistributionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(manifestRegistry);
        ArgumentNullException.ThrowIfNull(exposureEventStore);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new StringBuilder();
        AppendMetric(builder, "sharpninja_distribution_manifests", "Registered manifests visible to the Distribution service.", "gauge", manifestRegistry.Count);
        AppendMetric(builder, "sharpninja_distribution_auth_success_total", "Distribution requests accepted by product API key authorization.", "counter", Read(ref authSuccessTotal));
        AppendMetric(builder, "sharpninja_distribution_auth_failure_total", "Distribution requests rejected by product API key authorization.", "counter", Read(ref authFailureTotal));
        AppendMetric(builder, "sharpninja_distribution_attestation_success_total", "Distribution requests accepted by device attestation.", "counter", Read(ref attestationSuccessTotal));
        AppendMetric(builder, "sharpninja_distribution_attestation_failure_total", "Distribution requests rejected by device attestation.", "counter", Read(ref attestationFailureTotal));
        AppendMetric(builder, "sharpninja_distribution_attestation_skipped_total", "Distribution requests where device attestation was not required.", "counter", Read(ref attestationSkippedTotal));
        AppendMetric(builder, "sharpninja_distribution_manifest_cache_hits_total", "Distribution manifest lookups that found an origin manifest.", "counter", Read(ref manifestCacheHitsTotal));
        AppendMetric(builder, "sharpninja_distribution_manifest_cache_misses_total", "Distribution manifest lookups that missed the origin manifest store.", "counter", Read(ref manifestCacheMissesTotal));
        AppendMetric(builder, "sharpninja_distribution_manifest_not_modified_total", "Distribution manifest requests answered with HTTP 304.", "counter", Read(ref manifestNotModifiedTotal));
        AppendMetric(builder, "sharpninja_distribution_exposure_batches_total", "Exposure batches accepted by the Distribution service.", "counter", Read(ref exposureBatchesTotal));
        AppendMetric(builder, "sharpninja_distribution_exposure_events_total", "Exposure events accepted by the Distribution service.", "counter", Math.Max(exposureEventStore.Count, Read(ref exposureEventsTotal)));
        AppendStorageMode(builder, "manifest", options.Value.StorageMode);
        AppendStorageMode(builder, "exposure", options.Value.StorageMode);
        return builder.ToString();
    }

    private static long Read(ref long value) => Interlocked.Read(ref value);

    private static void AppendMetric(StringBuilder builder, string name, string help, string type, long value)
    {
        builder.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        builder.Append("# TYPE ").Append(name).Append(' ').AppendLine(type);
        builder.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).AppendLine();
    }

    private static void AppendStorageMode(StringBuilder builder, string store, SharpNinjaDistributionStorageMode mode)
    {
        builder
            .Append("sharpninja_distribution_storage_mode{store=\"")
            .Append(store)
            .Append("\",mode=\"")
            .Append(mode.ToString().ToLowerInvariant())
            .Append("\"} 1")
            .AppendLine();
    }
}
