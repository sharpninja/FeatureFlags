namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>Observed data for one variant in an experiment.</summary>
/// <param name="VariantKey">Variant key.</param>
/// <param name="Impressions">Number of subjects assigned to this variant.</param>
/// <param name="Conversions">Number of subjects who converted (binary metric).</param>
public sealed record ExperimentObservation(string VariantKey, long Impressions, long Conversions);
