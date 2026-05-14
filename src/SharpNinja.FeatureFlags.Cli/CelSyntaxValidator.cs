using System.Globalization;

namespace SharpNinja.FeatureFlags.Cli;

internal static class CelSyntaxValidator
{
    private static readonly HashSet<string> AllowedFunctions = new(StringComparer.Ordinal)
    {
        "all",
        "bucket",
        "exists",
        "filter",
        "map",
        "semver_satisfies",
        "version_compare",
    };

    internal static bool TryValidate(string expression, out string message)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            message = "CEL expression must be non-empty.";
            return false;
        }

        if (expression.Contains("=~", StringComparison.Ordinal)
            || expression.Contains("matches(", StringComparison.Ordinal)
            || expression.Contains(".matches(", StringComparison.Ordinal))
        {
            message = "CEL regular-expression operators and matches() calls are forbidden in v1.";
            return false;
        }

        Stack<char> delimiters = [];
        TokenKind lastToken = TokenKind.None;
        for (int index = 0; index < expression.Length;)
        {
            char current = expression[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current is '\'' or '"')
            {
                if (!TryReadString(expression, ref index, current, out message))
                {
                    return false;
                }

                lastToken = TokenKind.Operand;
                continue;
            }

            if (IsIdentifierStart(current))
            {
                string identifier = ReadIdentifier(expression, ref index);
                if (string.Equals(identifier, "in", StringComparison.Ordinal))
                {
                    lastToken = TokenKind.Operator;
                    continue;
                }

                int lookahead = SkipWhitespace(expression, index);
                if (lookahead < expression.Length && expression[lookahead] == '(' && !AllowedFunctions.Contains(identifier))
                {
                    message = string.Create(
                        CultureInfo.InvariantCulture,
                        $"CEL function '{identifier}' is not supported in v1.");
                    return false;
                }

                lastToken = TokenKind.Operand;
                continue;
            }

            if (char.IsDigit(current))
            {
                ReadNumber(expression, ref index);
                lastToken = TokenKind.Operand;
                continue;
            }

            if (current is '(' or '[' or '{')
            {
                delimiters.Push(MatchingClose(current));
                lastToken = TokenKind.OpenDelimiter;
                index++;
                continue;
            }

            if (current is ')' or ']' or '}')
            {
                if (delimiters.Count == 0 || delimiters.Pop() != current)
                {
                    message = string.Create(
                        CultureInfo.InvariantCulture,
                        $"CEL expression has an unmatched '{current}' delimiter.");
                    return false;
                }

                lastToken = TokenKind.Operand;
                index++;
                continue;
            }

            if (current == ',')
            {
                lastToken = TokenKind.Operator;
                index++;
                continue;
            }

            if (current is '?' or ':')
            {
                lastToken = TokenKind.Operator;
                index++;
                continue;
            }

            if (TryReadOperator(expression, ref index))
            {
                lastToken = TokenKind.Operator;
                continue;
            }

            message = string.Create(
                CultureInfo.InvariantCulture,
                $"CEL expression contains unsupported character '{current}'.");
            return false;
        }

        if (delimiters.Count > 0)
        {
            message = "CEL expression has an unclosed delimiter.";
            return false;
        }

        if (lastToken is TokenKind.None or TokenKind.Operator or TokenKind.OpenDelimiter)
        {
            message = "CEL expression is incomplete.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryReadString(string expression, ref int index, char quote, out string message)
    {
        index++;
        bool escaped = false;
        while (index < expression.Length)
        {
            char current = expression[index++];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == quote)
            {
                message = string.Empty;
                return true;
            }
        }

        message = "CEL string literal is not terminated.";
        return false;
    }

    private static string ReadIdentifier(string expression, ref int index)
    {
        int start = index;
        index++;
        while (index < expression.Length && IsIdentifierPart(expression[index]))
        {
            index++;
        }

        return expression[start..index];
    }

    private static void ReadNumber(string expression, ref int index)
    {
        index++;
        while (index < expression.Length && (char.IsDigit(expression[index]) || expression[index] == '.'))
        {
            index++;
        }
    }

    private static bool TryReadOperator(string expression, ref int index)
    {
        if (index + 1 < expression.Length)
        {
            string twoCharacterOperator = expression.Substring(index, 2);
            if (twoCharacterOperator is "==" or "!=" or "<=" or ">=" or "&&" or "||")
            {
                index += 2;
                return true;
            }
        }

        if (expression[index] is '<' or '>' or '+' or '-' or '*' or '/' or '%' or '!')
        {
            index++;
            return true;
        }

        return false;
    }

    private static int SkipWhitespace(string expression, int index)
    {
        while (index < expression.Length && char.IsWhiteSpace(expression[index]))
        {
            index++;
        }

        return index;
    }

    private static char MatchingClose(char open) =>
        open switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            _ => throw new ArgumentOutOfRangeException(nameof(open), open, "Unexpected opening delimiter."),
        };

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '.';

    private enum TokenKind
    {
        None,
        Operand,
        Operator,
        OpenDelimiter,
    }
}
