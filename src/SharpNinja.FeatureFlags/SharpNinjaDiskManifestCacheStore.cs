using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags;

internal sealed class SharpNinjaDiskManifestCacheStore : ISharpNinjaManifestCacheStore
{
    private static readonly Action<ILogger, string, Exception?> ManifestCacheReadFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1003, nameof(ManifestCacheReadFailed)),
            "Feature flag manifest cache could not be read from {ManifestCachePath}.");

    private readonly ILogger<SharpNinjaDiskManifestCacheStore> logger;
    private readonly SharpNinjaFeatureFlagOptions options;

    public SharpNinjaDiskManifestCacheStore(
        SharpNinjaFeatureFlagOptions options,
        ILogger<SharpNinjaDiskManifestCacheStore> logger)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SignedManifestEnvelope? Read()
    {
        string path = ResolvePath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;

            var envelope = new SignedManifestEnvelope(
                ReadRequiredString(root, "manifestJson"),
                ReadRequiredString(root, "signature"),
                ReadRequiredString(root, "signingKeyId"),
                ReadRequiredString(root, "algorithm"))
            {
                ManifestId = ReadOptionalString(root, "manifestId")
                    ?? SignedManifestEnvelopeDefaults.ManifestId,
                ETag = ReadOptionalString(root, "eTag"),
                PublishedAt = ReadOptionalDateTimeOffset(root, "publishedAt"),
            };

            if (string.Equals(envelope.ManifestId, SignedManifestEnvelopeDefaults.ManifestId, StringComparison.Ordinal))
            {
                envelope = envelope with { ManifestId = new SignedManifestEnvelope(
                    envelope.ManifestJson,
                    envelope.Signature,
                    envelope.SigningKeyId,
                    envelope.Algorithm).ManifestId };
            }

            return envelope.Validate();
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
        {
            ManifestCacheReadFailed(logger, path, exception);
            return null;
        }
    }

    public void Write(SignedManifestEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        envelope.Validate();

        string path = ResolvePath();
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("manifestJson", envelope.ManifestJson);
            writer.WriteString("signature", envelope.Signature);
            writer.WriteString("signingKeyId", envelope.SigningKeyId);
            writer.WriteString("algorithm", envelope.Algorithm);
            writer.WriteString("manifestId", envelope.ManifestId);
            WriteOptionalString(writer, "eTag", envelope.ETag);
            if (envelope.PublishedAt is DateTimeOffset publishedAt)
            {
                writer.WriteString("publishedAt", publishedAt);
            }

            writer.WriteEndObject();
        }

        File.WriteAllBytes(path, stream.ToArray());
    }

    private string ResolvePath()
    {
        if (!string.IsNullOrWhiteSpace(options.ManifestCachePath))
        {
            return options.ManifestCachePath;
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
            "manifest-cache.json");
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

    private static string ReadRequiredString(JsonElement root, string propertyName) =>
        root.GetProperty(propertyName).GetString() ?? string.Empty;

    private static string? ReadOptionalString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) ? value.GetString() : null;

    private static DateTimeOffset? ReadOptionalDateTimeOffset(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) && value.TryGetDateTimeOffset(out DateTimeOffset result)
            ? result
            : null;

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static class SignedManifestEnvelopeDefaults
    {
        public const string ManifestId = "__compute__";
    }
}
