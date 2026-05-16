namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>out-of-v1: one variant in an A/B/n experiment.</summary>
/// <param name="Key">Variant key (e.g. "control", "treatment-a").</param>
/// <param name="Weight">Relative weight (sum of all weights defines allocation ratio).</param>
public sealed record ExperimentVariant(string Key, double Weight);
