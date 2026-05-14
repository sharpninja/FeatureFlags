using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed record DistributionManifest(
    string ProductId,
    string ReleaseId,
    string Environment,
    string Json,
    string ETag,
    DateTimeOffset UpdatedAt)
{
    public static DistributionManifest FromJson(string json, DateTimeOffset? updatedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string productId = ReadRequiredString(root, "productId");
        string releaseId = ReadRequiredString(root, "releaseId");
        string environment = ReadRequiredString(root, "environment");

        return new DistributionManifest(
            productId,
            releaseId,
            environment,
            json,
            CreateETag(json),
            updatedAt ?? DateTimeOffset.UtcNow);
    }

    public static bool MatchesETag(string candidate, string entityTag)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        foreach (string token in candidate.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token == "*")
            {
                return true;
            }

            if (string.Equals(NormalizeETag(token), NormalizeETag(entityTag), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsNotModifiedSince(string? since)
    {
        if (string.IsNullOrWhiteSpace(since))
        {
            return false;
        }

        if (MatchesETag(since, ETag))
        {
            return true;
        }

        return DateTimeOffset.TryParse(
                since,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset sinceTimestamp)
            && sinceTimestamp >= UpdatedAt;
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new JsonException($"Manifest is missing required property '{propertyName}'.");
        }

        string? result = value.GetString();
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new JsonException($"Manifest property '{propertyName}' must not be blank.");
        }

        return result;
    }

    private static string CreateETag(string json)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return string.Create(CultureInfo.InvariantCulture, $"\"sha256-{Convert.ToHexString(hash)}\"");
    }

    private static string NormalizeETag(string entityTag)
    {
        string normalized = entityTag.Trim();
        if (normalized.StartsWith("W/", StringComparison.Ordinal))
        {
            normalized = normalized[2..].Trim();
        }

        return normalized;
    }
}
