using SharpNinja.FeatureFlags.Experimentation;
using Xunit;

namespace SharpNinja.FeatureFlags.Experimentation.Tests;

/// <summary>
/// Tests for <see cref="ExperimentAnalyzer"/> covering the two-proportion z-test
/// implementation, lift calculation, and edge case handling.
/// </summary>
public sealed class ExperimentAnalyzerTests
{
    private static readonly ExperimentAnalyzer Analyzer = new();

    /// <summary>
    /// With a large sample (1000 subjects each) and a notable effect (10% vs 20% conversion),
    /// the p-value must be less than 0.05, indicating statistical significance.
    /// </summary>
    [Fact]
    public void AnalyzeReturnsSignificantResultForLargeEffect()
    {
        var observations = new List<ExperimentObservation>
        {
            new ExperimentObservation("control", Impressions: 1000, Conversions: 100),   // 10%
            new ExperimentObservation("treatment", Impressions: 1000, Conversions: 200), // 20%
        };

        ExperimentAnalysisResult result = Analyzer.Analyze("exp-1", "control", observations);

        VariantAnalysisResult treatment = result.VariantResults.Single(r => r.VariantKey == "treatment");
        Assert.True(treatment.PValue < 0.05, $"Expected p < 0.05, got {treatment.PValue}");
        Assert.True(treatment.IsStatisticallySignificant);
        Assert.True(treatment.RelativeLift > 0, "Treatment lift should be positive.");
    }

    /// <summary>
    /// With a small sample (20 subjects each) and similar conversion rates,
    /// the p-value must be at or above 0.05, indicating no statistical significance.
    /// </summary>
    [Fact]
    public void AnalyzeReturnsNotSignificantForSmallSample()
    {
        var observations = new List<ExperimentObservation>
        {
            new ExperimentObservation("control", Impressions: 20, Conversions: 10),   // 50%
            new ExperimentObservation("treatment", Impressions: 20, Conversions: 11), // 55%
        };

        ExperimentAnalysisResult result = Analyzer.Analyze("exp-2", "control", observations);

        VariantAnalysisResult treatment = result.VariantResults.Single(r => r.VariantKey == "treatment");
        Assert.False(treatment.IsStatisticallySignificant);
        Assert.True(treatment.PValue >= 0.05, $"Expected p >= 0.05, got {treatment.PValue}");
    }

    /// <summary>
    /// When control and treatment have identical conversion rates,
    /// relative lift must be exactly 0 (or effectively 0 within floating-point tolerance).
    /// </summary>
    [Fact]
    public void AnalyzeReturnsZeroLiftForIdenticalRates()
    {
        var observations = new List<ExperimentObservation>
        {
            new ExperimentObservation("control", Impressions: 500, Conversions: 100),
            new ExperimentObservation("treatment", Impressions: 500, Conversions: 100),
        };

        ExperimentAnalysisResult result = Analyzer.Analyze("exp-3", "control", observations);

        VariantAnalysisResult treatment = result.VariantResults.Single(r => r.VariantKey == "treatment");
        Assert.Equal(0.0, treatment.RelativeLift, precision: 10);
    }

    /// <summary>
    /// When the specified control variant key is not present in the observations list,
    /// the analyzer must throw an <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void AnalyzeThrowsWhenControlVariantNotFound()
    {
        var observations = new List<ExperimentObservation>
        {
            new ExperimentObservation("treatment-a", Impressions: 200, Conversions: 40),
            new ExperimentObservation("treatment-b", Impressions: 200, Conversions: 50),
        };

        Assert.Throws<ArgumentException>(() =>
            Analyzer.Analyze("exp-4", "control", observations));
    }
}
