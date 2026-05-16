using System.Globalization;
using System.Text.Json;

namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>FR-8 TR-7 TR-9 v1 exposure upload batch accepted by the Distribution service.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
/// <param name="ProductId">Product identifier that emitted the exposure events.</param>
/// <param name="ReleaseId">Release identifier that emitted the exposure events.</param>
/// <param name="Environment">Deployment environment that emitted the exposure events.</param>
/// <param name="Events">Exposure events in the batch.</param>
public sealed record ExposureBatchRequest(
    string ProductId,
    string ReleaseId,
    string Environment,
    IReadOnlyList<ExposureEventRequest> Events)
{
    /// <summary>FR-8 parses an exposure upload request from JSON.</summary>
    /// <param name="root">Request JSON root element.</param>
    /// <param name="defaultEnvironment">Default environment when the batch omits one.</param>
    /// <returns>The parsed exposure batch.</returns>
    public static ExposureBatchRequest Parse(JsonElement root, string defaultEnvironment)
    {
        string productId = ReadRequiredString(root, "productId");
        string releaseId = ReadRequiredString(root, "releaseId");
        string environment = ReadOptionalString(root, "environment") ?? defaultEnvironment;

        if (!root.TryGetProperty("events", out JsonElement eventsElement)
            || eventsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Exposure batch requires an events array.");
        }

        List<ExposureEventRequest> events = [];
        foreach (JsonElement eventElement in eventsElement.EnumerateArray())
        {
            events.Add(ParseEvent(eventElement));
        }

        if (events.Count == 0)
        {
            throw new JsonException("Exposure batch requires at least one event.");
        }

        return new ExposureBatchRequest(productId, releaseId, environment, events);
    }

    private static ExposureEventRequest ParseEvent(JsonElement eventElement)
    {
        string flagKey = ReadRequiredString(eventElement, "flagKey");
        JsonElement resolvedValue = ReadRequiredElement(eventElement, "resolvedValue").Clone();
        int? matchedRuleIndex = ReadOptionalInt32(eventElement, "matchedRuleIndex");
        string contextFingerprint = ReadRequiredString(eventElement, "contextFingerprint");
        DateTimeOffset timestamp = ReadRequiredTimestamp(eventElement, "timestamp");

        return new ExposureEventRequest(
            flagKey,
            resolvedValue,
            matchedRuleIndex,
            contextFingerprint,
            timestamp);
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
        string? value = ReadOptionalString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"Exposure batch is missing required property '{propertyName}'.");
        }

        return value;
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.GetString();
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

    private static DateTimeOffset ReadRequiredTimestamp(JsonElement root, string propertyName)
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

/// <summary>FR-8 v1 single exposure event submitted by an SDK upload worker.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// </remarks>
/// <param name="FlagKey">Evaluated flag key.</param>
/// <param name="ResolvedValue">Resolved flag value.</param>
/// <param name="MatchedRuleIndex">Optional zero-based rule index that matched.</param>
/// <param name="ContextFingerprint">Stable evaluation context fingerprint.</param>
/// <param name="Timestamp">UTC timestamp captured by the SDK.</param>
public sealed record ExposureEventRequest(
    string FlagKey,
    JsonElement ResolvedValue,
    int? MatchedRuleIndex,
    string ContextFingerprint,
    DateTimeOffset Timestamp);
