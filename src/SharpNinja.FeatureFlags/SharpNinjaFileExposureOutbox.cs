using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaFileExposureOutbox : ISharpNinjaExposureOutbox
{
    private static readonly Action<ILogger, string, Exception?> ExposureOutboxReadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1005, nameof(ExposureOutboxReadFailed)),
            "Feature flag exposure outbox could not be read from {ExposureOutboxPath}.");

    private readonly List<SharpNinjaExposureEvent> events;
    private readonly Lock gate = new();
    private readonly ILogger<SharpNinjaFileExposureOutbox> logger;
    private readonly SharpNinjaFeatureFlagOptions options;

    public SharpNinjaFileExposureOutbox(
        SharpNinjaFeatureFlagOptions options,
        ILogger<SharpNinjaFileExposureOutbox> logger)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        events = LoadEvents();
    }

    public void Record(SharpNinjaExposureEvent exposureEvent)
    {
        ArgumentNullException.ThrowIfNull(exposureEvent);

        lock (gate)
        {
            events.Add(exposureEvent);
            TrimExpiredEvents();
            Persist();
        }
    }

    public IReadOnlyList<SharpNinjaExposureEvent> Snapshot()
    {
        lock (gate)
        {
            return new ReadOnlyCollection<SharpNinjaExposureEvent>([.. events]);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            events.Clear();
            Persist();
        }
    }

    public IReadOnlyList<SharpNinjaExposureEvent> DequeueBatch(int maximumCount)
    {
        if (maximumCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount), maximumCount, "Batch size must be greater than zero.");
        }

        lock (gate)
        {
            int count = Math.Min(maximumCount, events.Count);
            if (count == 0)
            {
                return Array.Empty<SharpNinjaExposureEvent>();
            }

            SharpNinjaExposureEvent[] batch = events.GetRange(0, count).ToArray();
            events.RemoveRange(0, count);
            Persist();
            return batch;
        }
    }

    public void Requeue(IReadOnlyCollection<SharpNinjaExposureEvent> eventsToRequeue)
    {
        ArgumentNullException.ThrowIfNull(eventsToRequeue);

        if (eventsToRequeue.Count == 0)
        {
            return;
        }

        lock (gate)
        {
            events.InsertRange(0, eventsToRequeue);
            TrimExpiredEvents();
            Persist();
        }
    }

    private List<SharpNinjaExposureEvent> LoadEvents()
    {
        string path = ResolvePath();
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            var loadedEvents = new List<SharpNinjaExposureEvent>();
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                loadedEvents.Add(SharpNinjaExposureJson.ReadEvent(element));
            }

            return loadedEvents;
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        {
            ExposureOutboxReadFailed(logger, path, exception);
            return [];
        }
    }

    private void TrimExpiredEvents()
    {
        TimeSpan? retentionPeriod = options.ExposureRetention.RetentionPeriod;
        if (retentionPeriod is null)
        {
            return;
        }

        DateTimeOffset cutoff = DateTimeOffset.UtcNow.Subtract(retentionPeriod.Value);
        events.RemoveAll(exposureEvent => exposureEvent.Timestamp < cutoff);
    }

    private void Persist()
    {
        string path = ResolvePath();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        foreach (SharpNinjaExposureEvent exposureEvent in events)
        {
            SharpNinjaExposureJson.WriteEvent(writer, exposureEvent);
        }

        writer.WriteEndArray();
    }

    private string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(options.ExposureOutboxPath))
        {
            return options.ExposureOutboxPath;
        }

        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(
            root,
            "SharpNinja",
            "FeatureFlags",
            SanitizePathSegment(options.ProductId),
            SanitizePathSegment(options.ReleaseId),
            SanitizePathSegment(options.Environment),
            "exposure-outbox.json");
    }

    private static string SanitizePathSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        foreach (char character in value)
        {
            builder.Append(Array.IndexOf(invalidCharacters, character) >= 0 ? '_' : character);
        }

        return builder.ToString();
    }
}
