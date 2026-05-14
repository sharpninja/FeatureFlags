using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags;

/// <summary>FR-1 FR-6 FR-8 FR-10 TR-5 TR-7 TR-11 Phase 1 SDK client that evaluates feature flags from the active manifest.</summary>
public sealed class SharpNinjaFeatureClient : ISharpNinjaFeatureClient
{
    private const string ReleaseLineageContextKey = "ReleaseLineage";

    private readonly FeatureFlagEvaluator evaluator;
    private readonly ISharpNinjaExposureEventSink exposureEventSink;
    private readonly FeatureFlagManifest manifest;
    private readonly SharpNinjaFeatureFlagOptions options;
    private readonly TimeProvider timeProvider;

    /// <summary>Creates a new SDK client from DI-managed collaborators.</summary>
    /// <param name="evaluator">Feature flag evaluator.</param>
    /// <param name="manifest">Parsed feature flag manifest.</param>
    /// <param name="options">SDK feature flag options.</param>
    /// <param name="exposureEventSink">Exposure event sink.</param>
    /// <param name="timeProvider">Time provider for exposure timestamps.</param>
    public SharpNinjaFeatureClient(
        FeatureFlagEvaluator evaluator,
        FeatureFlagManifest manifest,
        SharpNinjaFeatureFlagOptions options,
        ISharpNinjaExposureEventSink exposureEventSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(exposureEventSink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.evaluator = evaluator;
        this.manifest = manifest;
        this.options = options;
        this.exposureEventSink = exposureEventSink;
        this.timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public EvaluationResult<T> Evaluate<T>(string key, T defaultValue, EvaluationContext? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        EvaluationContext effectiveContext = BuildEffectiveContext(context);
        EvaluationResult<T> result = evaluator.Evaluate(manifest, options.ProductId, key, defaultValue, effectiveContext);

        exposureEventSink.Record(new SharpNinjaExposureEvent(
            key,
            result.Value,
            result.Reason,
            result.RuleIndex,
            Fingerprint(effectiveContext),
            timeProvider.GetUtcNow(),
            options.ProductId,
            options.ReleaseId,
            options.Environment,
            ResolveTenantId(effectiveContext)));

        return result;
    }

    /// <inheritdoc />
    public ValueTask<EvaluationResult<T>> EvaluateAsync<T>(
        string key,
        T defaultValue,
        EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<EvaluationResult<T>>(Task.FromCanceled<EvaluationResult<T>>(cancellationToken));
        }

        return ValueTask.FromResult(Evaluate(key, defaultValue, context));
    }

    private EvaluationContext BuildEffectiveContext(EvaluationContext? callerContext)
    {
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        if (callerContext?.Values is not null)
        {
            foreach (KeyValuePair<string, object?> pair in callerContext.Values)
            {
                if (!IsImmutableIdentityKey(pair.Key))
                {
                    values[pair.Key] = pair.Value;
                }
            }
        }

        SharpNinjaReleaseLineage releaseLineage = options.ReleaseLineage;
        values[SharpNinjaEvaluationContextKeys.ProductId] = options.ProductId;
        values[SharpNinjaEvaluationContextKeys.ReleaseId] = options.ReleaseId;
        values[ReleaseLineageContextKey] = releaseLineage;
        values[SharpNinjaEvaluationContextKeys.SemanticVersion] = releaseLineage.SemanticVersion;
        values[SharpNinjaEvaluationContextKeys.ReleaseChannel] = releaseLineage.Channel.ToString();
        values[SharpNinjaEvaluationContextKeys.ReleaseBuild] = releaseLineage.Build;
        values[SharpNinjaEvaluationContextKeys.Environment] = options.Environment;

        if (options.MultiTenant.DefaultTenantId is not null)
        {
            values[options.MultiTenant.TenantContextKey] = options.MultiTenant.DefaultTenantId;
            values[SharpNinjaEvaluationContextKeys.TenantId] = options.MultiTenant.DefaultTenantId;
        }

        return new EvaluationContext(
            new ReadOnlyDictionary<string, object?>(values));
    }

    private bool IsImmutableIdentityKey(string key)
    {
        if (string.Equals(key, SharpNinjaEvaluationContextKeys.ProductId, StringComparison.Ordinal)
            || string.Equals(key, SharpNinjaEvaluationContextKeys.ReleaseId, StringComparison.Ordinal)
            || string.Equals(key, ReleaseLineageContextKey, StringComparison.Ordinal)
            || string.Equals(key, SharpNinjaEvaluationContextKeys.SemanticVersion, StringComparison.Ordinal)
            || string.Equals(key, SharpNinjaEvaluationContextKeys.ReleaseChannel, StringComparison.Ordinal)
            || string.Equals(key, SharpNinjaEvaluationContextKeys.ReleaseBuild, StringComparison.Ordinal)
            || string.Equals(key, SharpNinjaEvaluationContextKeys.Environment, StringComparison.Ordinal))
        {
            return true;
        }

        return options.MultiTenant.DefaultTenantId is not null
            && (string.Equals(key, SharpNinjaEvaluationContextKeys.TenantId, StringComparison.Ordinal)
                || string.Equals(key, options.MultiTenant.TenantContextKey, StringComparison.Ordinal));
    }

    private string? ResolveTenantId(EvaluationContext context)
    {
        if (options.MultiTenant.DefaultTenantId is not null)
        {
            return options.MultiTenant.DefaultTenantId;
        }

        if (TryReadContextString(context, options.MultiTenant.TenantContextKey, out string? tenantId))
        {
            return tenantId;
        }

        return string.Equals(
            options.MultiTenant.TenantContextKey,
            SharpNinjaEvaluationContextKeys.TenantId,
            StringComparison.Ordinal)
            ? null
            : TryReadContextString(context, SharpNinjaEvaluationContextKeys.TenantId, out tenantId)
                ? tenantId
                : null;
    }

    private static bool TryReadContextString(EvaluationContext context, string key, out string? value)
    {
        if (context.Values.TryGetValue(key, out object? rawValue) && rawValue is not null)
        {
            value = Convert.ToString(rawValue, CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private static string Fingerprint(EvaluationContext context)
    {
        var builder = new StringBuilder();
        AppendDictionary(builder, context.Values);

        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void AppendDictionary(StringBuilder builder, IReadOnlyDictionary<string, object?> values)
    {
        List<string> keys = [.. values.Keys];
        keys.Sort(StringComparer.Ordinal);

        foreach (string key in keys)
        {
            builder
                .Append(key.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(key)
                .Append('=');
            AppendValue(builder, values[key]);
            builder.Append(';');
        }
    }

    private static void AppendValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("<null>");
                break;
            case string stringValue:
                AppendScalar(builder, stringValue);
                break;
            case IFormattable formattable:
                AppendScalar(builder, formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                builder.Append('{');
                AppendDictionary(builder, readOnlyDictionary);
                builder.Append('}');
                break;
            case IDictionary<string, object?> dictionary:
                builder.Append('{');
                AppendDictionary(builder, new ReadOnlyDictionary<string, object?>(dictionary));
                builder.Append('}');
                break;
            default:
                AppendScalar(builder, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static void AppendScalar(StringBuilder builder, string value)
    {
        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
