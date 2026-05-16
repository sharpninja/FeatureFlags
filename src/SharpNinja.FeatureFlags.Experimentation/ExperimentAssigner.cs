using System.Text;
using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>
/// out-of-v1: assigns subjects to experiment variants using FNV-1a 64-bit deterministic bucketing.
/// Evaluates the experiment's feature flag gate before assigning variants.
/// </summary>
public sealed class ExperimentAssigner : IExperimentAssigner
{
    private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
    private const ulong FnvPrime64 = 1099511628211UL;

    private readonly ISharpNinjaFeatureClient _client;
    private readonly ISharpNinjaExposureEventSink? _sink;

    /// <summary>Initializes an <see cref="ExperimentAssigner"/>.</summary>
    /// <param name="client">Feature flag client used to evaluate the experiment gate.</param>
    /// <param name="sink">Optional exposure event sink for recording assignments.</param>
    public ExperimentAssigner(ISharpNinjaFeatureClient client, ISharpNinjaExposureEventSink? sink)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _sink = sink;
    }

    /// <inheritdoc/>
    public ExperimentAssignment Assign(
        ExperimentDefinition experiment,
        string subjectId,
        EvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        if (experiment.Variants.Count == 0)
        {
            throw new ArgumentException("Experiment must have at least one variant.", nameof(experiment));
        }

        // Evaluate the feature flag gate.
        EvaluationResult<bool> flagResult = _client.Evaluate<bool>(
            experiment.FlagKey,
            defaultValue: false,
            context: context);

        string controlVariantKey = experiment.Variants[0].Key;

        if (!flagResult.Value)
        {
            // Subject is not eligible; return control variant without exposure recording.
            return new ExperimentAssignment(
                experiment.ExperimentId,
                subjectId,
                controlVariantKey,
                IsEligible: false);
        }

        // Hash the composite key for deterministic bucketing.
        string bucketKey = string.Concat(experiment.ExperimentId, ":", subjectId);
        ulong hash = Fnv1a64(bucketKey);

        // Normalize hash to [0, 1).
        double bucket = (double)(hash % 1_000_000UL) / 1_000_000.0;

        // Compute total weight and walk thresholds.
        double totalWeight = 0.0;
        foreach (ExperimentVariant variant in experiment.Variants)
        {
            totalWeight += variant.Weight;
        }

        double cumulative = 0.0;
        string assignedVariant = controlVariantKey;
        foreach (ExperimentVariant variant in experiment.Variants)
        {
            cumulative += variant.Weight / totalWeight;
            if (bucket < cumulative)
            {
                assignedVariant = variant.Key;
                break;
            }
        }

        // Emit exposure event if a sink is registered.
        _sink?.Record(new SharpNinjaExposureEvent(
            FlagKey: experiment.FlagKey,
            ResolvedValue: assignedVariant,
            Reason: flagResult.Reason,
            RuleIndex: flagResult.RuleIndex,
            ContextFingerprint: ComputeContextFingerprint(context),
            Timestamp: DateTimeOffset.UtcNow,
            ProductId: string.Empty,
            ReleaseId: string.Empty,
            Environment: string.Empty,
            TenantId: null));

        return new ExperimentAssignment(
            experiment.ExperimentId,
            subjectId,
            assignedVariant,
            IsEligible: true);
    }

    private static ulong Fnv1a64(string input)
    {
        ulong hash = FnvOffsetBasis64;
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= FnvPrime64;
        }

        return hash;
    }

    private static string ComputeContextFingerprint(EvaluationContext? context)
    {
        if (context is null || context.Values.Count == 0)
        {
            return "empty";
        }

        ulong hash = FnvOffsetBasis64;
        foreach (KeyValuePair<string, object?> kvp in context.Values.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
            byte[] valueBytes = Encoding.UTF8.GetBytes(kvp.Value?.ToString() ?? string.Empty);
            foreach (byte b in keyBytes)
            {
                hash ^= b;
                hash *= FnvPrime64;
            }

            foreach (byte b in valueBytes)
            {
                hash ^= b;
                hash *= FnvPrime64;
            }
        }

        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
    }
}
