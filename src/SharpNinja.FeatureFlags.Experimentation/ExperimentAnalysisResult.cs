namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>out-of-v1: statistical analysis result comparing variants against a control.</summary>
/// <param name="ExperimentId">Experiment identifier.</param>
/// <param name="ControlVariantKey">The control variant key used as baseline.</param>
/// <param name="VariantResults">Per-variant statistical results.</param>
public sealed record ExperimentAnalysisResult(
    string ExperimentId,
    string ControlVariantKey,
    IReadOnlyList<VariantAnalysisResult> VariantResults);

/// <summary>out-of-v1: statistical result for one variant vs. control.</summary>
/// <param name="VariantKey">The variant key.</param>
/// <param name="ConversionRate">Conversion rate for this variant (conversions / impressions).</param>
/// <param name="RelativeLift">Relative lift vs. control: (variantRate - controlRate) / controlRate.</param>
/// <param name="PValue">Two-proportion z-test p-value.</param>
/// <param name="ConfidenceIntervalLow">95% confidence interval lower bound on lift.</param>
/// <param name="ConfidenceIntervalHigh">95% confidence interval upper bound on lift.</param>
/// <param name="IsStatisticallySignificant">True when p &lt; 0.05.</param>
public sealed record VariantAnalysisResult(
    string VariantKey,
    double ConversionRate,
    double RelativeLift,
    double PValue,
    double ConfidenceIntervalLow,
    double ConfidenceIntervalHigh,
    bool IsStatisticallySignificant);
