using System.Collections;
using System.Globalization;

namespace SharpNinja.FeatureFlags.Cqrs;

/// <summary>
/// TEST-CQRS-MCPSERVER-001; TR-11; TR-MCP-CQRS-007: Extension method for serialization of CQRS request and result objects.
/// Uses AOT-safe formatting for primitive values and falls back to <see cref="object.ToString"/>.
/// </summary>
public static class YamlExtensions
{
    /// <summary>Serializes simple objects to an AOT-safe YAML-like diagnostic string.</summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A diagnostic string, or the object's <see cref="object.ToString"/> fallback on error.</returns>
    public static string ToYaml(this object? obj)
    {
        if (obj is null) return string.Empty;
        return FormatValue(obj);
    }

    private static string FormatValue(object obj)
    {
        if (obj is string value)
        {
            return value;
        }

        if (obj is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        if (obj is IEnumerable enumerable)
        {
            return string.Join('\n', enumerable.Cast<object?>().Select(static item => $"- {item ?? string.Empty}"));
        }

        return obj.ToString() ?? string.Empty;
    }
}
