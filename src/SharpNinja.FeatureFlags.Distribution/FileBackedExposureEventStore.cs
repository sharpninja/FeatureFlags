using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class FileBackedExposureEventStore : IExposureEventStore, IDisposable
{
    private static readonly Action<ILogger, int, string, Exception?> ExposureEventsPersisted =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(4020, nameof(ExposureEventsPersisted)),
            "Persisted {ExposureEventCount} exposure event(s) to {ExposurePath}.");

    private static readonly Action<ILogger, string, Exception?> ExposureLineSkipped =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(4021, nameof(ExposureLineSkipped)),
            "Skipped invalid exposure event line in {ExposurePath}.");

    private readonly ConcurrentQueue<StoredExposureEvent> events = [];
    private readonly SemaphoreSlim appendLock = new(1, 1);
    private readonly string exposurePath;
    private readonly ILogger<FileBackedExposureEventStore> logger;
    private long eventCount;

    public FileBackedExposureEventStore(
        IOptions<SharpNinjaDistributionOptions> options,
        ILogger<FileBackedExposureEventStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        exposurePath = Path.Combine(options.Value.StorageRootPath, "exposures", "events.jsonl");
        this.logger = logger;
        LoadExistingEvents();
    }

    public long Count => Interlocked.Read(ref eventCount);

    public async ValueTask<int> AppendAsync(ExposureBatchRequest batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(Path.GetDirectoryName(exposurePath)!);
        DateTimeOffset receivedAt = DateTimeOffset.UtcNow;
        var storedEvents = new List<StoredExposureEvent>(batch.Events.Count);
        foreach (ExposureEventRequest exposureEvent in batch.Events)
        {
            storedEvents.Add(new StoredExposureEvent(
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

        var builder = new StringBuilder();
        foreach (StoredExposureEvent storedEvent in storedEvents)
        {
            builder.AppendLine(WriteJsonLine(storedEvent));
        }

        await appendLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(exposurePath, builder.ToString(), Encoding.UTF8, cancellationToken);
        }
        finally
        {
            appendLock.Release();
        }

        foreach (StoredExposureEvent storedEvent in storedEvents)
        {
            events.Enqueue(storedEvent);
        }

        Interlocked.Add(ref eventCount, storedEvents.Count);
        ExposureEventsPersisted(logger, storedEvents.Count, exposurePath, null);
        return storedEvents.Count;
    }

    public IReadOnlyList<StoredExposureEvent> Snapshot() => events.ToArray();

    public void Dispose() => appendLock.Dispose();

    private void LoadExistingEvents()
    {
        if (!File.Exists(exposurePath))
        {
            return;
        }

        foreach (string line in File.ReadLines(exposurePath, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                StoredExposureEvent storedEvent = ParseJsonLine(line);
                events.Enqueue(storedEvent);
                Interlocked.Increment(ref eventCount);
            }
            catch (JsonException ex)
            {
                ExposureLineSkipped(logger, exposurePath, ex);
            }
        }
    }

    private static string WriteJsonLine(StoredExposureEvent storedEvent)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("productId", storedEvent.ProductId);
            writer.WriteString("releaseId", storedEvent.ReleaseId);
            writer.WriteString("environment", storedEvent.Environment);
            writer.WriteString("flagKey", storedEvent.FlagKey);
            writer.WritePropertyName("resolvedValue");
            storedEvent.ResolvedValue.WriteTo(writer);
            if (storedEvent.MatchedRuleIndex is int matchedRuleIndex)
            {
                writer.WriteNumber("matchedRuleIndex", matchedRuleIndex);
            }
            else
            {
                writer.WriteNull("matchedRuleIndex");
            }

            writer.WriteString("contextFingerprint", storedEvent.ContextFingerprint);
            writer.WriteString("timestamp", storedEvent.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("receivedAt", storedEvent.ReceivedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static StoredExposureEvent ParseJsonLine(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;
        return new StoredExposureEvent(
            ReadRequiredString(root, "productId"),
            ReadRequiredString(root, "releaseId"),
            ReadRequiredString(root, "environment"),
            ReadRequiredString(root, "flagKey"),
            ReadRequiredElement(root, "resolvedValue").Clone(),
            ReadOptionalInt32(root, "matchedRuleIndex"),
            ReadRequiredTimestamp(root, "contextFingerprint", isTimestamp: false),
            ReadRequiredTimestampValue(root, "timestamp"),
            ReadRequiredTimestampValue(root, "receivedAt"));
    }

    private static JsonElement ReadRequiredElement(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new JsonException($"Exposure event is missing required property '{propertyName}'.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new JsonException($"Exposure event is missing required property '{propertyName}'.");
        }

        string? result = value.GetString();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new JsonException($"Exposure event property '{propertyName}' must not be blank.");
        }

        return result;
    }

    private static int? ReadOptionalInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.GetInt32();
    }

    private static string ReadRequiredTimestamp(JsonElement root, string propertyName, bool isTimestamp)
    {
        _ = isTimestamp;
        return ReadRequiredString(root, propertyName);
    }

    private static DateTimeOffset ReadRequiredTimestampValue(JsonElement root, string propertyName)
    {
        string value = ReadRequiredString(root, propertyName);
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestamp))
        {
            throw new JsonException($"Exposure event property '{propertyName}' must be an ISO-8601 timestamp.");
        }

        return timestamp;
    }
}
