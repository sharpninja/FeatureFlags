using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Evaluation;

/// <summary>FR-5 TR-2 v1 diagnostic emitted while validating a CEL rule predicate.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Message">Diagnostic message.</param>
/// <param name="Position">Zero-based character position in the predicate.</param>
public sealed record RulePredicateDiagnostic(string Code, string Message, int Position);

/// <summary>FR-5 TR-2 v1 validation result for a CEL rule predicate.</summary>
/// <param name="IsValid">Indicates whether the predicate can be parsed and evaluated as a boolean expression.</param>
/// <param name="Diagnostics">Validation diagnostics in deterministic order.</param>
public sealed record RulePredicateValidationResult(
    bool IsValid,
    IReadOnlyList<RulePredicateDiagnostic> Diagnostics);

/// <summary>FR-5 TR-2 v1 trim-safe validator for the supported CEL rule subset.</summary>
public static class RulePredicateValidator
{
    /// <summary>FR-5 validates CEL syntax and boolean predicate typing.</summary>
    /// <param name="predicate">CEL predicate text.</param>
    /// <returns>The validation result.</returns>
    public static RulePredicateValidationResult Validate(string predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (RulePredicateCompiler.TryCompile(predicate, out _, out RulePredicateDiagnostic? diagnostic))
        {
            return new RulePredicateValidationResult(
                true,
                Array.Empty<RulePredicateDiagnostic>());
        }

        return new RulePredicateValidationResult(
            false,
            new[] { diagnostic ?? RulePredicateDiagnostics.Syntax("Predicate is invalid.", 0) });
    }
}

internal static class RulePredicateCompiler
{
    private static readonly ConcurrentDictionary<string, RulePredicateProgram> ProgramCache = new(StringComparer.Ordinal);

    public static RulePredicateProgram Compile(string predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return ProgramCache.GetOrAdd(predicate, static value =>
        {
            if (!TryCompileUncached(value, out RulePredicateProgram? program, out RulePredicateDiagnostic? diagnostic))
            {
                throw new FormatException(diagnostic?.Message ?? "Rule predicate is invalid.");
            }

            return program ?? throw new FormatException("Rule predicate is invalid.");
        });
    }

    public static bool TryCompile(
        string predicate,
        out RulePredicateProgram? program,
        out RulePredicateDiagnostic? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (ProgramCache.TryGetValue(predicate, out program))
        {
            diagnostic = null;
            return true;
        }

        if (!TryCompileUncached(predicate, out program, out diagnostic))
        {
            return false;
        }

        if (program is null)
        {
            diagnostic = RulePredicateDiagnostics.Syntax("Predicate is invalid.", 0);
            return false;
        }

        ProgramCache.TryAdd(predicate, program);
        return true;
    }

    private static bool TryCompileUncached(
        string predicate,
        out RulePredicateProgram? program,
        out RulePredicateDiagnostic? diagnostic)
    {
        program = null;
        diagnostic = null;

        if (string.IsNullOrWhiteSpace(predicate))
        {
            diagnostic = RulePredicateDiagnostics.Syntax("Predicate must be a non-empty CEL expression.", 0);
            return false;
        }

        try
        {
            RuleExpression expression = new RulePredicateParser(predicate).Parse();
            RuleValueKind kind = expression.InferType();
            if (kind is not RuleValueKind.Boolean and not RuleValueKind.Unknown)
            {
                diagnostic = RulePredicateDiagnostics.Type(
                    "Predicate must evaluate to a boolean value.",
                    expression.Position);
                return false;
            }

            program = new RulePredicateProgram(expression);
            return true;
        }
        catch (RulePredicateParseException exception)
        {
            diagnostic = RulePredicateDiagnostics.Syntax(exception.Message, exception.Position);
            return false;
        }
        catch (RulePredicateTypeException exception)
        {
            diagnostic = RulePredicateDiagnostics.Type(exception.Message, exception.Position);
            return false;
        }
    }
}

internal static class RulePredicateDiagnostics
{
    public const string SyntaxCode = "FFCEL_SYNTAX";
    public const string TypeCode = "FFCEL_TYPE";

    public static RulePredicateDiagnostic Syntax(string message, int position) =>
        new(SyntaxCode, message, Math.Max(position, 0));

    public static RulePredicateDiagnostic Type(string message, int position) =>
        new(TypeCode, message, Math.Max(position, 0));
}

internal sealed class RulePredicateProgram
{
    private readonly RuleExpression _expression;

    public RulePredicateProgram(RuleExpression expression)
    {
        _expression = expression;
    }

    public bool Evaluate(
        EvaluationContext context,
        string productId,
        string releaseId,
        string flagKey)
    {
        var evaluationContext = new RuleEvaluationContext(context, productId, releaseId, flagKey);
        object? value = _expression.Evaluate(evaluationContext);
        if (value is bool boolean)
        {
            return boolean;
        }

        throw new RulePredicateEvaluationException("Predicate did not evaluate to a boolean value.");
    }
}

internal sealed class RuleEvaluationContext
{
    private readonly IReadOnlyDictionary<string, object?> _variables;

    public RuleEvaluationContext(
        EvaluationContext context,
        string productId,
        string releaseId,
        string flagKey)
        : this(context, productId, releaseId, flagKey, new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(StringComparer.Ordinal)))
    {
    }

    private RuleEvaluationContext(
        EvaluationContext context,
        string productId,
        string releaseId,
        string flagKey,
        IReadOnlyDictionary<string, object?> variables)
    {
        Context = context;
        ProductId = productId;
        ReleaseId = releaseId;
        FlagKey = flagKey;
        _variables = variables;
    }

    public EvaluationContext Context { get; }

    public string ProductId { get; }

    public string ReleaseId { get; }

    public string FlagKey { get; }

    public RuleEvaluationContext WithVariable(string name, object? value)
    {
        var variables = new Dictionary<string, object?>(_variables, StringComparer.Ordinal)
        {
            [name] = value,
        };

        return new RuleEvaluationContext(
            Context,
            ProductId,
            ReleaseId,
            FlagKey,
            new ReadOnlyDictionary<string, object?>(variables));
    }

    public bool TryResolvePath(IReadOnlyList<string> path, out object? value)
    {
        if (path.Count == 0)
        {
            value = null;
            return false;
        }

        if (path.Count > 1)
        {
            string fullPath = string.Join('.', path);
            if (Context.Values.TryGetValue(fullPath, out value))
            {
                return true;
            }
        }

        if (!TryResolveIdentifier(path[0], out object? current))
        {
            value = null;
            return false;
        }

        for (int index = 1; index < path.Count; index++)
        {
            if (!TryReadMember(current, path[index], out current))
            {
                value = null;
                return false;
            }
        }

        value = current;
        return true;
    }

    public static bool TryReadMember(object? instance, string name, out object? value) =>
        TryReadDictionaryMember(instance, name, out value);

    private bool TryResolveIdentifier(string name, out object? value)
    {
        if (_variables.TryGetValue(name, out value))
        {
            return true;
        }

        if (Context.Values.TryGetValue(name, out value))
        {
            return true;
        }

        if (string.Equals(name, "ProductId", StringComparison.Ordinal)
            || string.Equals(name, "productId", StringComparison.Ordinal))
        {
            value = ProductId;
            return true;
        }

        if (string.Equals(name, "ReleaseId", StringComparison.Ordinal)
            || string.Equals(name, "releaseId", StringComparison.Ordinal))
        {
            value = ReleaseId;
            return true;
        }

        if (string.Equals(name, "FlagKey", StringComparison.Ordinal)
            || string.Equals(name, "flagKey", StringComparison.Ordinal))
        {
            value = FlagKey;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadDictionaryMember(object? instance, string name, out object? value)
    {
        switch (instance)
        {
            case IReadOnlyDictionary<string, object?> readOnlyDictionary
                when readOnlyDictionary.TryGetValue(name, out value):
                return true;
            case IDictionary<string, object?> dictionary
                when dictionary.TryGetValue(name, out value):
                return true;
            case IReadOnlyDictionary<string, string> stringReadOnlyDictionary
                when stringReadOnlyDictionary.TryGetValue(name, out string? readOnlyStringValue):
                value = readOnlyStringValue;
                return true;
            case IDictionary<string, string> stringDictionary
                when stringDictionary.TryGetValue(name, out string? dictionaryStringValue):
                value = dictionaryStringValue;
                return true;
            default:
                value = null;
                return false;
        }
    }
}

internal enum RuleValueKind
{
    Unknown,
    Null,
    Boolean,
    String,
    Number,
    List,
}

internal abstract class RuleExpression
{
    protected RuleExpression(int position)
    {
        Position = position;
    }

    public int Position { get; }

    public abstract object? Evaluate(RuleEvaluationContext context);

    public abstract RuleValueKind InferType();
}

internal sealed class LiteralExpression : RuleExpression
{
    private readonly object? _value;
    private readonly RuleValueKind _kind;

    public LiteralExpression(object? value, RuleValueKind kind, int position)
        : base(position)
    {
        _value = value;
        _kind = kind;
    }

    public override object? Evaluate(RuleEvaluationContext context) => _value;

    public override RuleValueKind InferType() => _kind;
}

internal sealed class IdentifierExpression : RuleExpression
{
    private readonly string _name;

    public IdentifierExpression(string name, int position)
        : base(position)
    {
        _name = name;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        if (context.TryResolvePath(new[] { _name }, out object? value))
        {
            return value;
        }

        return null;
    }

    public override RuleValueKind InferType() => RuleValueKind.Unknown;

    public bool TryAppendPath(List<string> path)
    {
        path.Add(_name);
        return true;
    }
}

internal sealed class MemberAccessExpression : RuleExpression
{
    private readonly RuleExpression _target;
    private readonly string _memberName;

    public MemberAccessExpression(RuleExpression target, string memberName, int position)
        : base(position)
    {
        _target = target;
        _memberName = memberName;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        if (TryBuildPath(out IReadOnlyList<string> path)
            && context.TryResolvePath(path, out object? pathValue))
        {
            return pathValue;
        }

        object? target = _target.Evaluate(context);
        return RuleEvaluationContext.TryReadMember(target, _memberName, out object? value) ? value : null;
    }

    public override RuleValueKind InferType() => RuleValueKind.Unknown;

    public bool TryAppendPath(List<string> path)
    {
        if (_target is IdentifierExpression identifier && identifier.TryAppendPath(path))
        {
            path.Add(_memberName);
            return true;
        }

        if (_target is MemberAccessExpression memberAccess && memberAccess.TryAppendPath(path))
        {
            path.Add(_memberName);
            return true;
        }

        return false;
    }

    private bool TryBuildPath(out IReadOnlyList<string> path)
    {
        var segments = new List<string>();
        if (TryAppendPath(segments))
        {
            path = segments;
            return true;
        }

        path = Array.Empty<string>();
        return false;
    }
}

internal sealed class ListExpression : RuleExpression
{
    private readonly IReadOnlyList<RuleExpression> _items;

    public ListExpression(IReadOnlyList<RuleExpression> items, int position)
        : base(position)
    {
        _items = items;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        var values = new List<object?>(_items.Count);
        foreach (RuleExpression item in _items)
        {
            values.Add(item.Evaluate(context));
        }

        return values;
    }

    public override RuleValueKind InferType()
    {
        foreach (RuleExpression item in _items)
        {
            _ = item.InferType();
        }

        return RuleValueKind.List;
    }
}

internal enum UnaryOperator
{
    Not,
    Negate,
}

internal sealed class UnaryExpression : RuleExpression
{
    private readonly UnaryOperator _operator;
    private readonly RuleExpression _operand;

    public UnaryExpression(UnaryOperator @operator, RuleExpression operand, int position)
        : base(position)
    {
        _operator = @operator;
        _operand = operand;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        object? value = _operand.Evaluate(context);
        return _operator switch
        {
            UnaryOperator.Not => !RuleValueConversions.RequireBoolean(value),
            UnaryOperator.Negate => -RuleValueConversions.RequireNumber(value),
            _ => throw new RulePredicateEvaluationException("Unsupported unary operator."),
        };
    }

    public override RuleValueKind InferType()
    {
        RuleValueKind operand = _operand.InferType();
        return _operator switch
        {
            UnaryOperator.Not when operand is RuleValueKind.Boolean or RuleValueKind.Unknown => RuleValueKind.Boolean,
            UnaryOperator.Negate when operand is RuleValueKind.Number or RuleValueKind.Unknown => RuleValueKind.Number,
            UnaryOperator.Not => throw new RulePredicateTypeException("Logical not requires a boolean operand.", Position),
            UnaryOperator.Negate => throw new RulePredicateTypeException("Numeric negation requires a numeric operand.", Position),
            _ => RuleValueKind.Unknown,
        };
    }
}

internal enum BinaryOperator
{
    Or,
    And,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    In,
}

internal sealed class BinaryExpression : RuleExpression
{
    private readonly BinaryOperator _operator;
    private readonly RuleExpression _left;
    private readonly RuleExpression _right;

    public BinaryExpression(BinaryOperator @operator, RuleExpression left, RuleExpression right, int position)
        : base(position)
    {
        _operator = @operator;
        _left = left;
        _right = right;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        if (_operator == BinaryOperator.And)
        {
            return RuleValueConversions.RequireBoolean(_left.Evaluate(context))
                && RuleValueConversions.RequireBoolean(_right.Evaluate(context));
        }

        if (_operator == BinaryOperator.Or)
        {
            return RuleValueConversions.RequireBoolean(_left.Evaluate(context))
                || RuleValueConversions.RequireBoolean(_right.Evaluate(context));
        }

        object? left = _left.Evaluate(context);
        object? right = _right.Evaluate(context);
        return _operator switch
        {
            BinaryOperator.Equal => RuleValueConversions.AreEqual(left, right),
            BinaryOperator.NotEqual => !RuleValueConversions.AreEqual(left, right),
            BinaryOperator.LessThan => RuleValueConversions.Compare(left, right) < 0,
            BinaryOperator.LessThanOrEqual => RuleValueConversions.Compare(left, right) <= 0,
            BinaryOperator.GreaterThan => RuleValueConversions.Compare(left, right) > 0,
            BinaryOperator.GreaterThanOrEqual => RuleValueConversions.Compare(left, right) >= 0,
            BinaryOperator.Add => RuleValueConversions.Add(left, right),
            BinaryOperator.Subtract => RuleValueConversions.RequireNumber(left) - RuleValueConversions.RequireNumber(right),
            BinaryOperator.Multiply => RuleValueConversions.RequireNumber(left) * RuleValueConversions.RequireNumber(right),
            BinaryOperator.Divide => Divide(left, right),
            BinaryOperator.Modulo => Modulo(left, right),
            BinaryOperator.In => RuleValueConversions.Contains(right, left),
            _ => throw new RulePredicateEvaluationException("Unsupported binary operator."),
        };
    }

    public override RuleValueKind InferType()
    {
        RuleValueKind left = _left.InferType();
        RuleValueKind right = _right.InferType();

        return _operator switch
        {
            BinaryOperator.And or BinaryOperator.Or => InferBooleanOperator(left, right),
            BinaryOperator.Equal or BinaryOperator.NotEqual => RuleValueKind.Boolean,
            BinaryOperator.LessThan
                or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan
                or BinaryOperator.GreaterThanOrEqual => InferComparisonOperator(left, right),
            BinaryOperator.Add => InferAddOperator(left, right),
            BinaryOperator.Subtract
                or BinaryOperator.Multiply
                or BinaryOperator.Divide
                or BinaryOperator.Modulo => InferNumericOperator(left, right),
            BinaryOperator.In => InferMembershipOperator(right),
            _ => RuleValueKind.Unknown,
        };
    }

    private static object Divide(object? left, object? right)
    {
        decimal divisor = RuleValueConversions.RequireNumber(right);
        if (divisor == 0m)
        {
            throw new RulePredicateEvaluationException("Division by zero.");
        }

        return RuleValueConversions.RequireNumber(left) / divisor;
    }

    private static object Modulo(object? left, object? right)
    {
        decimal divisor = RuleValueConversions.RequireNumber(right);
        if (divisor == 0m)
        {
            throw new RulePredicateEvaluationException("Modulo by zero.");
        }

        return RuleValueConversions.RequireNumber(left) % divisor;
    }

    private RuleValueKind InferBooleanOperator(RuleValueKind left, RuleValueKind right)
    {
        if (IsCompatible(left, RuleValueKind.Boolean) && IsCompatible(right, RuleValueKind.Boolean))
        {
            return RuleValueKind.Boolean;
        }

        throw new RulePredicateTypeException("Boolean operators require boolean operands.", Position);
    }

    private RuleValueKind InferComparisonOperator(RuleValueKind left, RuleValueKind right)
    {
        if (left == RuleValueKind.Unknown || right == RuleValueKind.Unknown)
        {
            return RuleValueKind.Boolean;
        }

        if ((left == RuleValueKind.Number && right == RuleValueKind.Number)
            || (left == RuleValueKind.String && right == RuleValueKind.String))
        {
            return RuleValueKind.Boolean;
        }

        throw new RulePredicateTypeException("Comparison operators require matching string or numeric operands.", Position);
    }

    private RuleValueKind InferAddOperator(RuleValueKind left, RuleValueKind right)
    {
        if (left == RuleValueKind.String || right == RuleValueKind.String)
        {
            return RuleValueKind.String;
        }

        if (left == RuleValueKind.Number && right == RuleValueKind.Number)
        {
            return RuleValueKind.Number;
        }

        if (left == RuleValueKind.Unknown || right == RuleValueKind.Unknown)
        {
            return RuleValueKind.Unknown;
        }

        throw new RulePredicateTypeException("Addition requires numeric operands or string concatenation.", Position);
    }

    private RuleValueKind InferNumericOperator(RuleValueKind left, RuleValueKind right)
    {
        if (IsCompatible(left, RuleValueKind.Number) && IsCompatible(right, RuleValueKind.Number))
        {
            return RuleValueKind.Number;
        }

        throw new RulePredicateTypeException("Arithmetic operators require numeric operands.", Position);
    }

    private RuleValueKind InferMembershipOperator(RuleValueKind right)
    {
        if (right is RuleValueKind.List or RuleValueKind.Unknown)
        {
            return RuleValueKind.Boolean;
        }

        throw new RulePredicateTypeException("Membership requires a list or context collection on the right side.", Position);
    }

    private static bool IsCompatible(RuleValueKind actual, RuleValueKind expected) =>
        actual == RuleValueKind.Unknown || actual == expected;
}

/// <summary>FR-5 CEL ternary expression: condition ? thenBranch : elseBranch.</summary>
internal sealed class TernaryExpression : RuleExpression
{
    private readonly RuleExpression _condition;
    private readonly RuleExpression _thenBranch;
    private readonly RuleExpression _elseBranch;

    /// <summary>FR-5 constructs a ternary expression node.</summary>
    public TernaryExpression(
        RuleExpression condition,
        RuleExpression thenBranch,
        RuleExpression elseBranch,
        int position)
        : base(position)
    {
        _condition = condition;
        _thenBranch = thenBranch;
        _elseBranch = elseBranch;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        bool condition = RuleValueConversions.RequireBoolean(_condition.Evaluate(context));
        return condition ? _thenBranch.Evaluate(context) : _elseBranch.Evaluate(context);
    }

    public override RuleValueKind InferType()
    {
        RuleValueKind conditionKind = _condition.InferType();
        if (conditionKind is not RuleValueKind.Boolean and not RuleValueKind.Unknown)
        {
            throw new RulePredicateTypeException(
                "Ternary condition must evaluate to a boolean value.", Position);
        }

        RuleValueKind thenKind = _thenBranch.InferType();
        RuleValueKind elseKind = _elseBranch.InferType();

        if (thenKind == elseKind)
        {
            return thenKind;
        }

        if (thenKind == RuleValueKind.Unknown || elseKind == RuleValueKind.Unknown)
        {
            return thenKind == RuleValueKind.Unknown ? elseKind : thenKind;
        }

        return RuleValueKind.Unknown;
    }
}

internal sealed class FunctionExpression : RuleExpression
{
    private readonly string _functionName;
    private readonly IReadOnlyList<RuleExpression> _arguments;

    public FunctionExpression(string functionName, IReadOnlyList<RuleExpression> arguments, int position)
        : base(position)
    {
        _functionName = functionName;
        _arguments = arguments;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        return _functionName switch
        {
            "semver_compare" or "version_compare" or "semverCompare" => EvaluateSemverCompare(context),
            "semver_satisfies" or "semverSatisfies" => EvaluateSemverSatisfies(context),
            "bucket" or "percentage" or "percentage_bucket" or "percentageBucket" => EvaluateBucket(context),
            _ => throw new RulePredicateEvaluationException(string.Concat(
                "Unsupported function '",
                _functionName,
                "'.")),
        };
    }

    public override RuleValueKind InferType()
    {
        foreach (RuleExpression argument in _arguments)
        {
            _ = argument.InferType();
        }

        return _functionName switch
        {
            "semver_compare" or "version_compare" or "semverCompare" => RequireArgumentCount(2, RuleValueKind.Number),
            "semver_satisfies" or "semverSatisfies" => RequireArgumentCount(2, RuleValueKind.Boolean),
            "bucket" or "percentage" or "percentage_bucket" or "percentageBucket" => InferBucketType(),
            _ => throw new RulePredicateTypeException(
                string.Concat("Unsupported function '", _functionName, "'."), Position),
        };
    }

    private int EvaluateSemverCompare(RuleEvaluationContext context)
    {
        RequireRuntimeArgumentCount(2);
        string left = RuleValueConversions.RequireString(_arguments[0].Evaluate(context));
        string right = RuleValueConversions.RequireString(_arguments[1].Evaluate(context));
        return Semver.Compare(left, right);
    }

    private bool EvaluateSemverSatisfies(RuleEvaluationContext context)
    {
        RequireRuntimeArgumentCount(2);
        string version = RuleValueConversions.RequireString(_arguments[0].Evaluate(context));
        string constraint = RuleValueConversions.RequireString(_arguments[1].Evaluate(context));
        return Semver.Satisfies(version, constraint);
    }

    private object EvaluateBucket(RuleEvaluationContext context)
    {
        if (_arguments.Count is not (1 or 2))
        {
            throw new RulePredicateEvaluationException("bucket requires one or two arguments.");
        }

        return _arguments.Count == 1
            ? EvaluateBucketValue(context)
            : EvaluateBucketMatch(context);
    }

    private decimal EvaluateBucketValue(RuleEvaluationContext context)
    {
        object? discriminator = _arguments[0].Evaluate(context);
        return DeterministicBucket.Calculate(
            context.ProductId,
            context.ReleaseId,
            context.FlagKey,
            RuleValueConversions.ToInvariantString(discriminator));
    }

    private bool EvaluateBucketMatch(RuleEvaluationContext context)
    {
        decimal bucket = EvaluateBucketValue(context);
        decimal percentage = RuleValueConversions.RequireNumber(_arguments[1].Evaluate(context));
        if (percentage is < 0m or > 100m)
        {
            throw new RulePredicateEvaluationException("bucket percentage must be between 0 and 100.");
        }

        return bucket < percentage;
    }

    private RuleValueKind InferBucketType()
    {
        if (_arguments.Count == 1)
        {
            return RuleValueKind.Number;
        }

        if (_arguments.Count == 2)
        {
            RuleValueKind percentage = _arguments[1].InferType();
            if (percentage is RuleValueKind.Number or RuleValueKind.Unknown)
            {
                return RuleValueKind.Boolean;
            }
        }

        throw new RulePredicateTypeException("bucket requires one value argument and an optional numeric percentage.", Position);
    }

    private RuleValueKind RequireArgumentCount(int count, RuleValueKind returnKind)
    {
        if (_arguments.Count != count)
        {
            throw new RulePredicateTypeException(
                string.Concat(_functionName, " requires ", count.ToString(CultureInfo.InvariantCulture), " arguments."),
                Position);
        }

        return returnKind;
    }

    private void RequireRuntimeArgumentCount(int count)
    {
        if (_arguments.Count != count)
        {
            throw new RulePredicateEvaluationException(string.Concat(
                _functionName,
                " requires ",
                count.ToString(CultureInfo.InvariantCulture),
                " arguments."));
        }
    }
}

internal sealed class MacroExpression : RuleExpression
{
    private const int MaxIterations = 512;

    private readonly RuleExpression _target;
    private readonly string _macroName;
    private readonly string _variableName;
    private readonly RuleExpression _predicate;

    public MacroExpression(
        RuleExpression target,
        string macroName,
        string variableName,
        RuleExpression predicate,
        int position)
        : base(position)
    {
        _target = target;
        _macroName = macroName;
        _variableName = variableName;
        _predicate = predicate;
    }

    public override object? Evaluate(RuleEvaluationContext context)
    {
        object? target = _target.Evaluate(context);

        if (_macroName == "filter")
        {
            return EvaluateFilter(context, target);
        }

        if (_macroName == "map")
        {
            return EvaluateMap(context, target);
        }

        bool any = false;
        bool all = true;
        int matches = 0;
        int iterations = 0;

        foreach (object? item in RuleValueConversions.Enumerate(target))
        {
            iterations++;
            if (iterations > MaxIterations)
            {
                throw new RulePredicateEvaluationException("Macro iteration limit exceeded.");
            }

            any = true;
            bool predicateResult = RuleValueConversions.RequireBoolean(
                _predicate.Evaluate(context.WithVariable(_variableName, item)));

            if (predicateResult)
            {
                matches++;
            }
            else
            {
                all = false;
            }

            if (_macroName == "exists" && predicateResult)
            {
                return true;
            }

            if (_macroName == "all" && !predicateResult)
            {
                return false;
            }
        }

        return _macroName switch
        {
            "exists" => false,
            "all" => !any || all,
            "exists_one" or "existsOne" => matches == 1,
            _ => throw new RulePredicateEvaluationException(string.Concat(
                "Unsupported macro '",
                _macroName,
                "'.")),
        };
    }

    /// <summary>FR-5 filter(list, var, predicate) - returns a new list containing only elements for which predicate is true.</summary>
    private List<object?> EvaluateFilter(RuleEvaluationContext context, object? target)
    {
        var result = new List<object?>();
        int iterations = 0;
        foreach (object? item in RuleValueConversions.Enumerate(target))
        {
            iterations++;
            if (iterations > MaxIterations)
            {
                throw new RulePredicateEvaluationException("Macro iteration limit exceeded.");
            }

            bool matches = RuleValueConversions.RequireBoolean(
                _predicate.Evaluate(context.WithVariable(_variableName, item)));
            if (matches)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>FR-5 map(list, var, expr) - returns a new list by applying expr to each element.</summary>
    private List<object?> EvaluateMap(RuleEvaluationContext context, object? target)
    {
        var result = new List<object?>();
        int iterations = 0;
        foreach (object? item in RuleValueConversions.Enumerate(target))
        {
            iterations++;
            if (iterations > MaxIterations)
            {
                throw new RulePredicateEvaluationException("Macro iteration limit exceeded.");
            }

            result.Add(_predicate.Evaluate(context.WithVariable(_variableName, item)));
        }

        return result;
    }

    public override RuleValueKind InferType()
    {
        RuleValueKind target = _target.InferType();
        if (target is not RuleValueKind.List and not RuleValueKind.Unknown)
        {
            throw new RulePredicateTypeException("Macros require a list or context collection target.", Position);
        }

        if (_macroName == "filter" || _macroName == "map")
        {
            _ = _predicate.InferType();
            return RuleValueKind.List;
        }

        RuleValueKind predicate = _predicate.InferType();
        if (predicate is not RuleValueKind.Boolean and not RuleValueKind.Unknown)
        {
            throw new RulePredicateTypeException("Macro predicate must evaluate to boolean.", Position);
        }

        return _macroName switch
        {
            "exists" or "all" or "exists_one" or "existsOne" => RuleValueKind.Boolean,
            _ => throw new RulePredicateTypeException(
                string.Concat("Unsupported macro '", _macroName, "'."), Position),
        };
    }
}

internal static class RuleValueConversions
{
    public static bool RequireBoolean(object? value)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        throw new RulePredicateEvaluationException("Expected boolean value.");
    }

    public static string RequireString(object? value)
    {
        if (value is string text)
        {
            return text;
        }

        throw new RulePredicateEvaluationException("Expected string value.");
    }

    public static decimal RequireNumber(object? value)
    {
        if (TryConvertNumber(value, out decimal number))
        {
            return number;
        }

        throw new RulePredicateEvaluationException("Expected numeric value.");
    }

    public static object Add(object? left, object? right)
    {
        if (left is string || right is string)
        {
            return string.Concat(ToInvariantString(left), ToInvariantString(right));
        }

        return RequireNumber(left) + RequireNumber(right);
    }

    public static int Compare(object? left, object? right)
    {
        if (TryConvertNumber(left, out decimal leftNumber) && TryConvertNumber(right, out decimal rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (left is string leftString && right is string rightString)
        {
            return string.Compare(leftString, rightString, StringComparison.Ordinal);
        }

        throw new RulePredicateEvaluationException("Values are not comparable.");
    }

    public static bool AreEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryConvertNumber(left, out decimal leftNumber) && TryConvertNumber(right, out decimal rightNumber))
        {
            return leftNumber == rightNumber;
        }

        if (left is string leftString && right is string rightString)
        {
            return string.Equals(leftString, rightString, StringComparison.Ordinal);
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool == rightBool;
        }

        return left.Equals(right);
    }

    public static bool Contains(object? collection, object? expected)
    {
        foreach (object? item in Enumerate(collection))
        {
            if (AreEqual(item, expected))
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<object?> Enumerate(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is string)
        {
            throw new RulePredicateEvaluationException("String values are not enumerable for CEL membership.");
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        throw new RulePredicateEvaluationException("Expected list or enumerable value.");
    }

    public static string ToInvariantString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    private static bool TryConvertNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case byte typed:
                number = typed;
                return true;
            case sbyte typed:
                number = typed;
                return true;
            case short typed:
                number = typed;
                return true;
            case ushort typed:
                number = typed;
                return true;
            case int typed:
                number = typed;
                return true;
            case uint typed:
                number = typed;
                return true;
            case long typed:
                number = typed;
                return true;
            case ulong typed:
                number = typed;
                return true;
            case float typed when !float.IsNaN(typed) && !float.IsInfinity(typed):
                number = (decimal)typed;
                return true;
            case double typed when !double.IsNaN(typed) && !double.IsInfinity(typed):
                number = (decimal)typed;
                return true;
            case decimal typed:
                number = typed;
                return true;
            default:
                number = 0m;
                return false;
        }
    }
}

internal static class DeterministicBucket
{
    public static decimal Calculate(string productId, string releaseId, string flagKey, string discriminator)
    {
        ulong hash = 14695981039346656037UL;
        Append(ref hash, productId);
        Append(ref hash, "\n");
        Append(ref hash, releaseId);
        Append(ref hash, "\n");
        Append(ref hash, flagKey);
        Append(ref hash, "\n");
        Append(ref hash, discriminator);

        return (hash % 10000UL) / 100m;
    }

    private static void Append(ref ulong hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        for (int index = 0; index < bytes.Length; index++)
        {
            hash ^= bytes[index];
            hash *= 1099511628211UL;
        }
    }
}

internal static class Semver
{
    public static int Compare(string left, string right)
    {
        if (!SemanticVersion.TryParse(left, out SemanticVersion leftVersion))
        {
            throw new RulePredicateEvaluationException(string.Concat("Invalid semantic version '", left, "'."));
        }

        if (!SemanticVersion.TryParse(right, out SemanticVersion rightVersion))
        {
            throw new RulePredicateEvaluationException(string.Concat("Invalid semantic version '", right, "'."));
        }

        return leftVersion.CompareTo(rightVersion);
    }

    public static bool Satisfies(string version, string constraint)
    {
        string trimmed = constraint.Trim();
        string op = "==";
        string versionText = trimmed;

        foreach (string candidate in new[] { ">=", "<=", "==", "!=", ">", "<", "=" })
        {
            if (trimmed.StartsWith(candidate, StringComparison.Ordinal))
            {
                op = candidate == "=" ? "==" : candidate;
                versionText = trimmed[candidate.Length..].Trim();
                break;
            }
        }

        int comparison = Compare(version, versionText);
        return op switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            _ => false,
        };
    }
}

internal readonly struct SemanticVersion : IComparable<SemanticVersion>
{
    private readonly string[] _preRelease;

    private SemanticVersion(int major, int minor, int patch, string[] preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public static bool TryParse(string value, out SemanticVersion version)
    {
        version = default;
        string text = value.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] is 'v' or 'V')
        {
            text = text[1..];
        }

        int buildIndex = text.IndexOf('+', StringComparison.Ordinal);
        if (buildIndex >= 0)
        {
            text = text[..buildIndex];
        }

        string[] releaseParts = text.Split('-', 2, StringSplitOptions.None);
        string[] core = releaseParts[0].Split('.', StringSplitOptions.None);
        if (core.Length != 3
            || !TryParsePart(core[0], out int major)
            || !TryParsePart(core[1], out int minor)
            || !TryParsePart(core[2], out int patch))
        {
            return false;
        }

        string[] preRelease = releaseParts.Length == 2
            ? releaseParts[1].Split('.', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        version = new SemanticVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int core = Major.CompareTo(other.Major);
        if (core != 0)
        {
            return core;
        }

        core = Minor.CompareTo(other.Minor);
        if (core != 0)
        {
            return core;
        }

        core = Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        if (_preRelease.Length == 0 && other._preRelease.Length == 0)
        {
            return 0;
        }

        if (_preRelease.Length == 0)
        {
            return 1;
        }

        if (other._preRelease.Length == 0)
        {
            return -1;
        }

        int count = Math.Min(_preRelease.Length, other._preRelease.Length);
        for (int index = 0; index < count; index++)
        {
            int comparison = ComparePreReleaseIdentifier(_preRelease[index], other._preRelease[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    private static bool TryParsePart(string value, out int parsed)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed)
            && parsed >= 0;
    }

    private static int ComparePreReleaseIdentifier(string left, string right)
    {
        bool leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out int leftNumber);
        bool rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out int rightNumber);

        if (leftNumeric && rightNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return string.Compare(left, right, StringComparison.Ordinal);
    }
}

internal enum TokenKind
{
    End,
    Identifier,
    Number,
    String,
    True,
    False,
    Null,
    In,
    Or,
    And,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Bang,
    OpenParen,
    CloseParen,
    OpenBracket,
    CloseBracket,
    Comma,
    Dot,
    QuestionMark,
    Colon,
}

internal readonly record struct Token(TokenKind Kind, string Text, int Position);

internal sealed class RulePredicateParser
{
    private readonly RulePredicateLexer _lexer;
    private Token _current;

    public RulePredicateParser(string text)
    {
        _lexer = new RulePredicateLexer(text);
        _current = _lexer.Next();
    }

    public RuleExpression Parse()
    {
        RuleExpression expression = ParseTernary();
        Expect(TokenKind.End, "Unexpected token after expression.");
        return expression;
    }

    /// <summary>FR-5 parses a right-associative CEL ternary operator: condition ? thenBranch : elseBranch.</summary>
    private RuleExpression ParseTernary()
    {
        RuleExpression condition = ParseOr();
        if (!Match(TokenKind.QuestionMark, out Token questionMark))
        {
            return condition;
        }

        RuleExpression thenBranch = ParseTernary();
        Expect(TokenKind.Colon, "Expected ':' in ternary operator.");
        RuleExpression elseBranch = ParseTernary();
        return new TernaryExpression(condition, thenBranch, elseBranch, questionMark.Position);
    }

    private RuleExpression ParseOr()
    {
        RuleExpression expression = ParseAnd();
        while (Match(TokenKind.Or, out Token op))
        {
            expression = new BinaryExpression(BinaryOperator.Or, expression, ParseAnd(), op.Position);
        }

        return expression;
    }

    private RuleExpression ParseAnd()
    {
        RuleExpression expression = ParseEquality();
        while (Match(TokenKind.And, out Token op))
        {
            expression = new BinaryExpression(BinaryOperator.And, expression, ParseEquality(), op.Position);
        }

        return expression;
    }

    private RuleExpression ParseEquality()
    {
        RuleExpression expression = ParseComparison();
        while (true)
        {
            if (Match(TokenKind.Equal, out Token equal))
            {
                expression = new BinaryExpression(BinaryOperator.Equal, expression, ParseComparison(), equal.Position);
                continue;
            }

            if (Match(TokenKind.NotEqual, out Token notEqual))
            {
                expression = new BinaryExpression(BinaryOperator.NotEqual, expression, ParseComparison(), notEqual.Position);
                continue;
            }

            if (Match(TokenKind.In, out Token membership))
            {
                expression = new BinaryExpression(BinaryOperator.In, expression, ParseComparison(), membership.Position);
                continue;
            }

            return expression;
        }
    }

    private RuleExpression ParseComparison()
    {
        RuleExpression expression = ParseAdditive();
        while (true)
        {
            if (Match(TokenKind.LessThan, out Token lessThan))
            {
                expression = new BinaryExpression(BinaryOperator.LessThan, expression, ParseAdditive(), lessThan.Position);
                continue;
            }

            if (Match(TokenKind.LessThanOrEqual, out Token lessThanOrEqual))
            {
                expression = new BinaryExpression(
                    BinaryOperator.LessThanOrEqual,
                    expression,
                    ParseAdditive(),
                    lessThanOrEqual.Position);
                continue;
            }

            if (Match(TokenKind.GreaterThan, out Token greaterThan))
            {
                expression = new BinaryExpression(BinaryOperator.GreaterThan, expression, ParseAdditive(), greaterThan.Position);
                continue;
            }

            if (Match(TokenKind.GreaterThanOrEqual, out Token greaterThanOrEqual))
            {
                expression = new BinaryExpression(
                    BinaryOperator.GreaterThanOrEqual,
                    expression,
                    ParseAdditive(),
                    greaterThanOrEqual.Position);
                continue;
            }

            return expression;
        }
    }

    private RuleExpression ParseAdditive()
    {
        RuleExpression expression = ParseMultiplicative();
        while (true)
        {
            if (Match(TokenKind.Plus, out Token plus))
            {
                expression = new BinaryExpression(BinaryOperator.Add, expression, ParseMultiplicative(), plus.Position);
                continue;
            }

            if (Match(TokenKind.Minus, out Token minus))
            {
                expression = new BinaryExpression(BinaryOperator.Subtract, expression, ParseMultiplicative(), minus.Position);
                continue;
            }

            return expression;
        }
    }

    private RuleExpression ParseMultiplicative()
    {
        RuleExpression expression = ParseUnary();
        while (true)
        {
            if (Match(TokenKind.Star, out Token star))
            {
                expression = new BinaryExpression(BinaryOperator.Multiply, expression, ParseUnary(), star.Position);
                continue;
            }

            if (Match(TokenKind.Slash, out Token slash))
            {
                expression = new BinaryExpression(BinaryOperator.Divide, expression, ParseUnary(), slash.Position);
                continue;
            }

            if (Match(TokenKind.Percent, out Token percent))
            {
                expression = new BinaryExpression(BinaryOperator.Modulo, expression, ParseUnary(), percent.Position);
                continue;
            }

            return expression;
        }
    }

    private RuleExpression ParseUnary()
    {
        if (Match(TokenKind.Bang, out Token bang))
        {
            return new UnaryExpression(UnaryOperator.Not, ParseUnary(), bang.Position);
        }

        if (Match(TokenKind.Minus, out Token minus))
        {
            return new UnaryExpression(UnaryOperator.Negate, ParseUnary(), minus.Position);
        }

        return ParsePostfix();
    }

    private RuleExpression ParsePostfix()
    {
        RuleExpression expression = ParsePrimary();
        while (Match(TokenKind.Dot, out Token dot))
        {
            Token member = Expect(TokenKind.Identifier, "Expected member name after '.'.");
            if (IsMacroName(member.Text) && Match(TokenKind.OpenParen, out _))
            {
                Token variable = Expect(TokenKind.Identifier, "Expected macro variable name.");
                Expect(TokenKind.Comma, "Expected ',' after macro variable name.");
                RuleExpression predicate = ParseTernary();
                Expect(TokenKind.CloseParen, "Expected ')' after macro predicate.");
                expression = new MacroExpression(expression, member.Text, variable.Text, predicate, dot.Position);
                continue;
            }

            expression = new MemberAccessExpression(expression, member.Text, dot.Position);
        }

        return expression;
    }

    private RuleExpression ParsePrimary()
    {
        if (Match(TokenKind.True, out Token trueToken))
        {
            return new LiteralExpression(true, RuleValueKind.Boolean, trueToken.Position);
        }

        if (Match(TokenKind.False, out Token falseToken))
        {
            return new LiteralExpression(false, RuleValueKind.Boolean, falseToken.Position);
        }

        if (Match(TokenKind.Null, out Token nullToken))
        {
            return new LiteralExpression(null, RuleValueKind.Null, nullToken.Position);
        }

        if (Match(TokenKind.Number, out Token number))
        {
            return new LiteralExpression(
                decimal.Parse(number.Text, NumberStyles.Number, CultureInfo.InvariantCulture),
                RuleValueKind.Number,
                number.Position);
        }

        if (Match(TokenKind.String, out Token text))
        {
            return new LiteralExpression(text.Text, RuleValueKind.String, text.Position);
        }

        if (Match(TokenKind.Identifier, out Token identifier))
        {
            if (Match(TokenKind.OpenParen, out _))
            {
                return ParseFunction(identifier);
            }

            return new IdentifierExpression(identifier.Text, identifier.Position);
        }

        if (Match(TokenKind.OpenBracket, out Token bracket))
        {
            return ParseList(bracket.Position);
        }

        if (Match(TokenKind.OpenParen, out _))
        {
            RuleExpression expression = ParseTernary();
            Expect(TokenKind.CloseParen, "Expected ')' after expression.");
            return expression;
        }

        throw Error("Expected expression.");
    }

    private FunctionExpression ParseFunction(Token identifier)
    {
        var arguments = new List<RuleExpression>();
        if (!Match(TokenKind.CloseParen, out _))
        {
            do
            {
                arguments.Add(ParseTernary());
            }
            while (Match(TokenKind.Comma, out _));

            Expect(TokenKind.CloseParen, "Expected ')' after function arguments.");
        }

        return new FunctionExpression(identifier.Text, new ReadOnlyCollection<RuleExpression>(arguments), identifier.Position);
    }

    private ListExpression ParseList(int position)
    {
        var items = new List<RuleExpression>();
        if (!Match(TokenKind.CloseBracket, out _))
        {
            do
            {
                items.Add(ParseTernary());
            }
            while (Match(TokenKind.Comma, out _));

            Expect(TokenKind.CloseBracket, "Expected ']' after list literal.");
        }

        return new ListExpression(new ReadOnlyCollection<RuleExpression>(items), position);
    }

    private bool Match(TokenKind kind, out Token token)
    {
        if (_current.Kind == kind)
        {
            token = _current;
            _current = _lexer.Next();
            return true;
        }

        token = default;
        return false;
    }

    private Token Expect(TokenKind kind, string message)
    {
        if (Match(kind, out Token token))
        {
            return token;
        }

        throw Error(message);
    }

    private static bool IsMacroName(string name) =>
        string.Equals(name, "exists", StringComparison.Ordinal)
        || string.Equals(name, "all", StringComparison.Ordinal)
        || string.Equals(name, "exists_one", StringComparison.Ordinal)
        || string.Equals(name, "existsOne", StringComparison.Ordinal)
        || string.Equals(name, "filter", StringComparison.Ordinal)
        || string.Equals(name, "map", StringComparison.Ordinal);

    private RulePredicateParseException Error(string message) =>
        new(message, _current.Position);
}

internal sealed class RulePredicateLexer
{
    private readonly string _text;
    private int _position;

    public RulePredicateLexer(string text)
    {
        _text = text;
    }

    public Token Next()
    {
        SkipWhitespace();
        if (_position >= _text.Length)
        {
            return new Token(TokenKind.End, string.Empty, _position);
        }

        int start = _position;
        char current = _text[_position];
        if (IsIdentifierStart(current))
        {
            return ReadIdentifierOrKeyword(start);
        }

        if (char.IsDigit(current))
        {
            return ReadNumber(start);
        }

        if (current is '\'' or '"')
        {
            return ReadString(start, current);
        }

        _position++;
        return current switch
        {
            '(' => new Token(TokenKind.OpenParen, "(", start),
            ')' => new Token(TokenKind.CloseParen, ")", start),
            '[' => new Token(TokenKind.OpenBracket, "[", start),
            ']' => new Token(TokenKind.CloseBracket, "]", start),
            ',' => new Token(TokenKind.Comma, ",", start),
            '.' => new Token(TokenKind.Dot, ".", start),
            '+' => new Token(TokenKind.Plus, "+", start),
            '-' => new Token(TokenKind.Minus, "-", start),
            '*' => new Token(TokenKind.Star, "*", start),
            '/' => new Token(TokenKind.Slash, "/", start),
            '%' => new Token(TokenKind.Percent, "%", start),
            '!' when TryConsume('=') => new Token(TokenKind.NotEqual, "!=", start),
            '!' => new Token(TokenKind.Bang, "!", start),
            '=' when TryConsume('=') => new Token(TokenKind.Equal, "==", start),
            '<' when TryConsume('=') => new Token(TokenKind.LessThanOrEqual, "<=", start),
            '<' => new Token(TokenKind.LessThan, "<", start),
            '>' when TryConsume('=') => new Token(TokenKind.GreaterThanOrEqual, ">=", start),
            '>' => new Token(TokenKind.GreaterThan, ">", start),
            '&' when TryConsume('&') => new Token(TokenKind.And, "&&", start),
            '|' when TryConsume('|') => new Token(TokenKind.Or, "||", start),
            '?' => new Token(TokenKind.QuestionMark, "?", start),
            ':' => new Token(TokenKind.Colon, ":", start),
            _ => throw new RulePredicateParseException(
                string.Concat("Unexpected character '", current.ToString(), "'."), start),
        };
    }

    private Token ReadIdentifierOrKeyword(int start)
    {
        while (_position < _text.Length && IsIdentifierPart(_text[_position]))
        {
            _position++;
        }

        string text = _text[start.._position];
        return text switch
        {
            "true" => new Token(TokenKind.True, text, start),
            "false" => new Token(TokenKind.False, text, start),
            "null" => new Token(TokenKind.Null, text, start),
            "in" => new Token(TokenKind.In, text, start),
            _ => new Token(TokenKind.Identifier, text, start),
        };
    }

    private Token ReadNumber(int start)
    {
        while (_position < _text.Length && char.IsDigit(_text[_position]))
        {
            _position++;
        }

        if (_position + 1 < _text.Length && _text[_position] == '.' && char.IsDigit(_text[_position + 1]))
        {
            _position++;
            while (_position < _text.Length && char.IsDigit(_text[_position]))
            {
                _position++;
            }
        }

        return new Token(TokenKind.Number, _text[start.._position], start);
    }

    private Token ReadString(int start, char quote)
    {
        _position++;
        var builder = new StringBuilder();
        while (_position < _text.Length)
        {
            char current = _text[_position++];
            if (current == quote)
            {
                return new Token(TokenKind.String, builder.ToString(), start);
            }

            if (current == '\\')
            {
                if (_position >= _text.Length)
                {
                    throw new RulePredicateParseException("Unterminated string escape.", _position);
                }

                current = _text[_position++];
                builder.Append(current switch
                {
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => current,
                });
                continue;
            }

            builder.Append(current);
        }

        throw new RulePredicateParseException("Unterminated string literal.", start);
    }

    private bool TryConsume(char expected)
    {
        if (_position < _text.Length && _text[_position] == expected)
        {
            _position++;
            return true;
        }

        return false;
    }

    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_';
}

internal sealed class RulePredicateParseException : Exception
{
    public RulePredicateParseException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    public int Position { get; }
}

internal sealed class RulePredicateTypeException : Exception
{
    public RulePredicateTypeException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    public int Position { get; }
}

internal sealed class RulePredicateEvaluationException : Exception
{
    public RulePredicateEvaluationException(string message)
        : base(message)
    {
    }
}
