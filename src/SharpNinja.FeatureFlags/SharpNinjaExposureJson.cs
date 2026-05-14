using System.Globalization;
using System.Text.Json;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags;

internal static class SharpNinjaExposureJson
{
    public static SharpNinjaExposureEvent ReadEvent(JsonElement element)
    {
        return new SharpNinjaExposureEvent(
            ReadRequiredString(element, "flagKey"),
            ReadValue(element.GetProperty("resolvedValue")),
            ReadReason(element),
            ReadOptionalInt32(element, "ruleIndex"),
            ReadRequiredString(element, "contextFingerprint"),
            element.GetProperty("timestamp").GetDateTimeOffset(),
            ReadRequiredString(element, "productId"),
            ReadRequiredString(element, "releaseId"),
            ReadRequiredString(element, "environment"),
            ReadOptionalString(element, "tenantId"));
    }

    public static void WriteEvent(Utf8JsonWriter writer, SharpNinjaExposureEvent exposureEvent)
    {
        writer.WriteStartObject();
        writer.WriteString("flagKey", exposureEvent.FlagKey);
        writer.WritePropertyName("resolvedValue");
        WriteValue(writer, exposureEvent.ResolvedValue);
        writer.WriteString("reason", exposureEvent.Reason.ToString());
        if (exposureEvent.RuleIndex is int ruleIndex)
        {
            writer.WriteNumber("ruleIndex", ruleIndex);
        }
        else
        {
            writer.WriteNull("ruleIndex");
        }

        writer.WriteString("contextFingerprint", exposureEvent.ContextFingerprint);
        writer.WriteString("timestamp", exposureEvent.Timestamp);
        writer.WriteString("productId", exposureEvent.ProductId);
        writer.WriteString("releaseId", exposureEvent.ReleaseId);
        writer.WriteString("environment", exposureEvent.Environment);
        if (exposureEvent.TenantId is null)
        {
            writer.WriteNull("tenantId");
        }
        else
        {
            writer.WriteString("tenantId", exposureEvent.TenantId);
        }

        writer.WriteEndObject();
    }

    private static EvaluationReason ReadReason(JsonElement element)
    {
        string reason = ReadRequiredString(element, "reason");
        return Enum.TryParse(reason, ignoreCase: false, out EvaluationReason parsed)
            ? parsed
            : EvaluationReason.Error;
    }

    private static object? ReadValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out long longValue) => longValue,
            JsonValueKind.Number when value.TryGetDouble(out double doubleValue) => doubleValue,
            JsonValueKind.Object or JsonValueKind.Array => value.Clone(),
            _ => null,
        };
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonElement jsonElement:
                jsonElement.WriteTo(writer);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static string ReadRequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString() ?? string.Empty;

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.GetString();
    }

    private static int? ReadOptionalInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.TryGetInt32(out int result) ? result : null;
    }
}
