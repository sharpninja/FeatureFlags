namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>Performs statistical analysis on experiment observation data.</summary>
public interface IExperimentAnalyzer
{
    /// <summary>
    /// Analyzes experiment observations using a two-proportion z-test for each
    /// treatment variant vs. the specified control.
    /// </summary>
    /// <param name="experimentId">The experiment identifier.</param>
    /// <param name="controlVariantKey">The variant key to use as the control baseline.</param>
    /// <param name="observations">Per-variant impression and conversion counts.</param>
    /// <returns>Statistical results for every variant including the control.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="controlVariantKey"/> is not present in <paramref name="observations"/>.
    /// </exception>
    ExperimentAnalysisResult Analyze(
        string experimentId,
        string controlVariantKey,
        IReadOnlyList<ExperimentObservation> observations);
}
