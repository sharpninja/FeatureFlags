using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Evaluation;

/// <summary>TR-11 evaluates version 1 manifest-backed feature flags without global OpenFeature state.</summary>
public sealed class FeatureFlagEvaluator
{
    private static readonly Action<ILogger, string, Exception?> FlagNotFound =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1, nameof(FlagNotFound)),
            "Feature flag '{FeatureFlagKey}' was not found.");

    private static readonly Action<ILogger, string, string, Exception?> ProductScopeDenied =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, nameof(ProductScopeDenied)),
            "Feature flag '{FeatureFlagKey}' denied product '{ProductId}' by product scope.");

    private static readonly Action<ILogger, string, string, string, Exception?> TypeMismatch =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Debug,
            new EventId(3, nameof(TypeMismatch)),
            "Feature flag '{FeatureFlagKey}' type '{FeatureFlagType}' is incompatible with requested type '{RequestedType}'.");

    private readonly ILogger<FeatureFlagEvaluator> _logger;

    /// <summary>Creates a feature flag evaluator.</summary>
    /// <param name="logger">Typed evaluator logger.</param>
    public FeatureFlagEvaluator(ILogger<FeatureFlagEvaluator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>TR-11 evaluates a feature flag from a parsed manifest.</summary>
    /// <typeparam name="T">Expected feature flag value type.</typeparam>
    /// <param name="manifest">Parsed feature flag manifest.</param>
    /// <param name="productId">Requesting product identifier.</param>
    /// <param name="key">Feature flag key.</param>
    /// <param name="defaultValue">Caller fallback value.</param>
    /// <param name="context">Optional evaluation context.</param>
    /// <returns>The evaluation result.</returns>
    public EvaluationResult<T> Evaluate<T>(
        FeatureFlagManifest manifest,
        string productId,
        string key,
        T defaultValue,
        EvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        FeatureFlagDefinition? flag = FindFlag(manifest, key);
        if (flag is null)
        {
            FlagNotFound(_logger, key, null);
            return new EvaluationResult<T>(
                defaultValue,
                EvaluationReason.Default,
                ErrorMessage: string.Concat("Feature flag '", key, "' was not found."));
        }

        if (!ProductScopeContains(flag, productId))
        {
            ProductScopeDenied(_logger, key, productId, null);
            return new EvaluationResult<T>(
                defaultValue,
                EvaluationReason.Default,
                ErrorMessage: string.Concat(
                    "Product '",
                    productId,
                    "' is not in productScope for feature flag '",
                    key,
                    "'."));
        }

        if (!IsRequestedTypeCompatible<T>(flag.Type))
        {
            string requestedTypeName = typeof(T).FullName ?? typeof(T).Name;
            TypeMismatch(_logger, key, flag.Type, requestedTypeName, null);
            return new EvaluationResult<T>(
                defaultValue,
                EvaluationReason.Error,
                ErrorMessage: string.Concat(
                    "Feature flag '",
                    key,
                    "' type '",
                    flag.Type,
                    "' is incompatible with requested type '",
                    requestedTypeName,
                    "'."));
        }

        EvaluationContext effectiveContext = context ?? EvaluationContext.Empty;
        for (int ruleIndex = 0; ruleIndex < flag.Rules.Count; ruleIndex++)
        {
            FeatureFlagRule rule = flag.Rules[ruleIndex];
            if (!RulePredicateMatcher.IsMatch(rule.When, effectiveContext))
            {
                continue;
            }

            if (TryConvertValue(rule.Value, out T ruleValue))
            {
                return new EvaluationResult<T>(ruleValue, EvaluationReason.RuleMatch, RuleIndex: ruleIndex);
            }

            return new EvaluationResult<T>(
                defaultValue,
                EvaluationReason.Error,
                ErrorMessage: string.Concat("Feature flag '", key, "' rule value could not be converted."),
                RuleIndex: ruleIndex);
        }

        if (TryConvertValue(flag.DefaultValue, out T manifestDefaultValue))
        {
            return new EvaluationResult<T>(manifestDefaultValue, EvaluationReason.Default);
        }

        return new EvaluationResult<T>(
            defaultValue,
            EvaluationReason.Error,
            ErrorMessage: string.Concat("Feature flag '", key, "' defaultValue could not be converted."));
    }

    private static FeatureFlagDefinition? FindFlag(FeatureFlagManifest manifest, string key)
    {
        foreach (FeatureFlagDefinition flag in manifest.Flags)
        {
            if (string.Equals(flag.Key, key, StringComparison.Ordinal))
            {
                return flag;
            }
        }

        return null;
    }

    private static bool ProductScopeContains(FeatureFlagDefinition flag, string productId)
    {
        foreach (string scopedProductId in flag.ProductScope)
        {
            if (string.Equals(scopedProductId, productId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRequestedTypeCompatible<T>(string manifestType)
    {
        Type requestedType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        return manifestType switch
        {
            "boolean" => requestedType == typeof(bool),
            "string" => requestedType == typeof(string),
            "integer" => requestedType == typeof(int) || requestedType == typeof(long),
            "number" => requestedType == typeof(double)
                || requestedType == typeof(float)
                || requestedType == typeof(decimal),
            _ => false,
        };
    }

    private static bool TryConvertValue<T>(object? value, out T converted)
    {
        Type requestedType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (value is null)
        {
            converted = default!;
            return !requestedType.IsValueType || Nullable.GetUnderlyingType(typeof(T)) is not null;
        }

        if (value is T typed)
        {
            converted = typed;
            return true;
        }

        if (requestedType == typeof(int) && value is long longValue && longValue is >= int.MinValue and <= int.MaxValue)
        {
            converted = (T)(object)(int)longValue;
            return true;
        }

        if (requestedType == typeof(float) && value is double doubleValue)
        {
            converted = (T)(object)(float)doubleValue;
            return true;
        }

        if (requestedType == typeof(decimal) && value is double decimalSource)
        {
            converted = (T)(object)(decimal)decimalSource;
            return true;
        }

        converted = default!;
        return false;
    }
}
