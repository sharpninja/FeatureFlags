using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace SharpNinja.FeatureFlags.Evaluation;

/// <summary>TR-11 runtime manifest parsed from the version 1 feature flag manifest schema.</summary>
/// <param name="ProductId">Root product identifier.</param>
/// <param name="ReleaseId">Manifest release identifier.</param>
/// <param name="Environment">Manifest environment.</param>
/// <param name="Flags">Feature flag definitions.</param>
public sealed record FeatureFlagManifest(
    string ProductId,
    string ReleaseId,
    string Environment,
    IReadOnlyList<FeatureFlagDefinition> Flags)
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>TR-11 parses a validated version 1 manifest JSON payload.</summary>
    /// <param name="json">Manifest JSON text.</param>
    /// <returns>The parsed manifest.</returns>
    public static FeatureFlagManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using JsonDocument document = JsonDocument.Parse(json, JsonOptions);
        JsonElement root = document.RootElement;

        string productId = ReadRequiredString(root, "productId");
        string releaseId = ReadRequiredString(root, "releaseId");
        string environment = ReadRequiredString(root, "environment");

        List<FeatureFlagDefinition> flags = [];
        foreach (JsonElement flag in root.GetProperty("flags").EnumerateArray())
        {
            flags.Add(ParseFlag(flag));
        }

        return new FeatureFlagManifest(
            productId,
            releaseId,
            environment,
            new ReadOnlyCollection<FeatureFlagDefinition>(flags));
    }

    private static FeatureFlagDefinition ParseFlag(JsonElement flag)
    {
        string key = ReadRequiredString(flag, "key");
        string type = ReadRequiredString(flag, "type");
        object? defaultValue = ReadManifestValue(flag.GetProperty("defaultValue"), type);
        bool killable = flag.GetProperty("killable").GetBoolean();

        List<string> productScope = [];
        foreach (JsonElement product in flag.GetProperty("productScope").EnumerateArray())
        {
            productScope.Add(product.GetString() ?? string.Empty);
        }

        List<FeatureFlagRule> rules = [];
        if (flag.TryGetProperty("rules", out JsonElement rulesElement))
        {
            foreach (JsonElement rule in rulesElement.EnumerateArray())
            {
                rules.Add(new FeatureFlagRule(
                    ReadRequiredString(rule, "when"),
                    ReadManifestValue(rule.GetProperty("value"), type)));
            }
        }

        return new FeatureFlagDefinition(
            key,
            type,
            defaultValue,
            killable,
            new ReadOnlyCollection<string>(productScope),
            new ReadOnlyCollection<FeatureFlagRule>(rules));
    }

    private static string ReadRequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString() ?? string.Empty;

    private static object? ReadManifestValue(JsonElement value, string type) =>
        type switch
        {
            "boolean" => value.GetBoolean(),
            "string" => value.GetString(),
            "integer" => value.GetInt64(),
            "number" => value.GetDouble(),
            _ => throw new JsonException(string.Create(
                CultureInfo.InvariantCulture,
                $"Unsupported feature flag type '{type}'.")),
        };
}

/// <summary>TR-11 runtime feature flag definition from a parsed manifest.</summary>
/// <param name="Key">Feature flag key.</param>
/// <param name="Type">Feature flag value type.</param>
/// <param name="DefaultValue">Default manifest value.</param>
/// <param name="Killable">Indicates whether the flag supports kill-switch behavior.</param>
/// <param name="ProductScope">Product identifiers allowed to consume the flag.</param>
/// <param name="Rules">Ordered evaluation rules.</param>
public sealed record FeatureFlagDefinition(
    string Key,
    string Type,
    object? DefaultValue,
    bool Killable,
    IReadOnlyList<string> ProductScope,
    IReadOnlyList<FeatureFlagRule> Rules);

/// <summary>TR-11 runtime feature flag rule from a parsed manifest.</summary>
/// <param name="When">Rule predicate text.</param>
/// <param name="Value">Rule result value.</param>
public sealed record FeatureFlagRule(string When, object? Value);
