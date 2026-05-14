using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Evaluation;

internal static class RulePredicateMatcher
{
    public static bool IsMatch(string predicate, EvaluationContext context)
    {
        if (!TryParseEquality(predicate, out string? path, out string? expected))
        {
            return false;
        }

        return TryGetContextValue(context, path, out object? actual)
            && actual is not null
            && string.Equals(Convert.ToString(actual, System.Globalization.CultureInfo.InvariantCulture), expected, StringComparison.Ordinal);
    }

    private static bool TryParseEquality(string predicate, out string path, out string expected)
    {
        path = string.Empty;
        expected = string.Empty;

        int operatorIndex = predicate.IndexOf("==", StringComparison.Ordinal);
        if (operatorIndex <= 0)
        {
            return false;
        }

        string left = predicate[..operatorIndex].Trim();
        string right = predicate[(operatorIndex + 2)..].Trim();
        if (left.Length == 0 || right.Length < 2)
        {
            return false;
        }

        char quote = right[0];
        if ((quote != '\'' && quote != '"') || right[^1] != quote)
        {
            return false;
        }

        string literal = right[1..^1];
        if (literal.Contains(quote, StringComparison.Ordinal))
        {
            return false;
        }

        path = left;
        expected = literal;
        return true;
    }

    private static bool TryGetContextValue(EvaluationContext context, string path, out object? value)
    {
        if (context.Values.TryGetValue(path, out value))
        {
            return true;
        }

        string[] segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1 || !context.Values.TryGetValue(segments[0], out object? current))
        {
            value = null;
            return false;
        }

        for (int index = 1; index < segments.Length; index++)
        {
            if (current is IReadOnlyDictionary<string, object?> readOnlyDictionary
                && readOnlyDictionary.TryGetValue(segments[index], out current))
            {
                continue;
            }

            if (current is IDictionary<string, object?> dictionary
                && dictionary.TryGetValue(segments[index], out current))
            {
                continue;
            }

            value = null;
            return false;
        }

        value = current;
        return true;
    }
}
