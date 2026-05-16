using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>Assigns subjects to experiment variants deterministically.</summary>
public interface IExperimentAssigner
{
    /// <summary>Assigns a subject to an experiment variant deterministically.</summary>
    /// <param name="experiment">The experiment definition containing variants and the flag key.</param>
    /// <param name="subjectId">The subject identifier (user/device ID) used for bucketing.</param>
    /// <param name="context">Optional evaluation context passed to the feature flag.</param>
    /// <returns>The deterministic variant assignment for the subject.</returns>
    ExperimentAssignment Assign(ExperimentDefinition experiment, string subjectId, EvaluationContext? context = null);
}
