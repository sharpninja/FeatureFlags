using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-3 FR-6 TR-8 TR-9 v1 immutable Distribution manifest payload served to SDK clients.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-3"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-6"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
/// <param name="ProductId">Product identifier addressed by the manifest.</param>
/// <param name="ReleaseId">Release identifier addressed by the manifest.</param>
/// <param name="Environment">Deployment environment addressed by the manifest.</param>
/// <param name="Json">Canonical signed manifest JSON payload.</param>
/// <param name="ETag">Strong entity tag for CDN and SDK cache validation.</param>
/// <param name="UpdatedAt">UTC timestamp used for cache validators.</param>
public sealed record DistributionManifest(
    string ProductId,
    string ReleaseId,
    string Environment,
    string Json,
    string ETag,
    DateTimeOffset UpdatedAt)
{
    /// <summary>FR-3 parses and validates the addressing tuple from a manifest JSON document.</summary>
    /// <param name="json">Manifest JSON document.</param>
    /// <param name="updatedAt">Optional update timestamp.</param>
    /// <returns>A distribution manifest ready for storage.</returns>
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

    /// <summary>FR-3 checks whether a client entity-tag value matches the manifest entity tag.</summary>
    /// <param name="candidate">Client entity-tag header value.</param>
    /// <param name="entityTag">Manifest entity tag.</param>
    /// <returns><see langword="true"/> when the values match.</returns>
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

    /// <summary>FR-3 checks whether a delta request can be answered as not modified.</summary>
    /// <param name="since">Client delta validator, either an entity tag or timestamp.</param>
    /// <returns><see langword="true"/> when the client already has the current manifest.</returns>
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
