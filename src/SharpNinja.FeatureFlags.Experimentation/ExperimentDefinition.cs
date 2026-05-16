namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>Defines an A/B/n experiment backed by a feature flag.</summary>
/// <param name="ExperimentId">Unique experiment identifier.</param>
/// <param name="FlagKey">The feature flag key that gates this experiment.</param>
/// <param name="Variants">Ordered list of variants. Weights are normalized to sum to 1.</param>
public sealed record ExperimentDefinition(
    string ExperimentId,
    string FlagKey,
    IReadOnlyList<ExperimentVariant> Variants);
