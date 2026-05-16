namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>Result of assigning a subject to an experiment variant.</summary>
/// <param name="ExperimentId">The experiment identifier.</param>
/// <param name="SubjectId">The subject identifier (user/device ID).</param>
/// <param name="VariantKey">The assigned variant key.</param>
/// <param name="IsEligible">False if the flag gate is disabled for this subject.</param>
public sealed record ExperimentAssignment(
    string ExperimentId,
    string SubjectId,
    string VariantKey,
    bool IsEligible);
