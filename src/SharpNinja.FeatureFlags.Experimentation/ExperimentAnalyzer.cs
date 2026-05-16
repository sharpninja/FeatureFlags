namespace SharpNinja.FeatureFlags.Experimentation;

/// <summary>
/// out-of-v1: default <see cref="IExperimentAnalyzer"/> implementation that performs a
/// two-proportion z-test for each treatment variant against a designated control,
/// reporting conversion rate, relative lift, two-tailed p-value, and a 95% confidence
/// interval on the absolute rate difference.
/// </summary>
public sealed record class ExperimentAnalyzer : IExperimentAnalyzer
{
    private const double SignificanceLevel = 0.05;
    private const double ZCriticalNinetyFive = 1.959963984540054;

    /// <inheritdoc />
    public ExperimentAnalysisResult Analyze(
        string experimentId,
        string controlVariantKey,
        IReadOnlyList<ExperimentObservation> observations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(experimentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(controlVariantKey);
        ArgumentNullException.ThrowIfNull(observations);

        ExperimentObservation? control = observations.FirstOrDefault(o =>
            string.Equals(o.VariantKey, controlVariantKey, StringComparison.Ordinal));

        if (control is null)
        {
            throw new ArgumentException(
                $"Control variant '{controlVariantKey}' not found in observations.",
                nameof(controlVariantKey));
        }

        double controlRate = control.Impressions > 0
            ? (double)control.Conversions / control.Impressions
            : 0.0;

        var results = new List<VariantAnalysisResult>(observations.Count);
        foreach (ExperimentObservation observation in observations)
        {
            results.Add(ComputeVariantResult(observation, control, controlRate));
        }

        return new ExperimentAnalysisResult(experimentId, controlVariantKey, results);
    }

    private static VariantAnalysisResult ComputeVariantResult(
        ExperimentObservation variant,
        ExperimentObservation control,
        double controlRate)
    {
        double variantRate = variant.Impressions > 0
            ? (double)variant.Conversions / variant.Impressions
            : 0.0;

        bool isControl = string.Equals(variant.VariantKey, control.VariantKey, StringComparison.Ordinal);

        if (isControl)
        {
            return new VariantAnalysisResult(
                variant.VariantKey,
                ConversionRate: variantRate,
                RelativeLift: 0.0,
                PValue: 1.0,
                ConfidenceIntervalLow: 0.0,
                ConfidenceIntervalHigh: 0.0,
                IsStatisticallySignificant: false);
        }

        double relativeLift = controlRate > 0.0 ? (variantRate - controlRate) / controlRate : 0.0;

        double pValue = TwoProportionPValue(
            variantSuccesses: variant.Conversions,
            variantTrials: variant.Impressions,
            controlSuccesses: control.Conversions,
            controlTrials: control.Impressions);

        (double ciLow, double ciHigh) = AbsoluteDifferenceConfidenceInterval(
            variantRate, variant.Impressions, controlRate, control.Impressions);

        return new VariantAnalysisResult(
            variant.VariantKey,
            ConversionRate: variantRate,
            RelativeLift: relativeLift,
            PValue: pValue,
            ConfidenceIntervalLow: ciLow,
            ConfidenceIntervalHigh: ciHigh,
            IsStatisticallySignificant: pValue < SignificanceLevel);
    }

    private static double TwoProportionPValue(
        long variantSuccesses,
        long variantTrials,
        long controlSuccesses,
        long controlTrials)
    {
        if (variantTrials <= 0 || controlTrials <= 0)
        {
            return 1.0;
        }

        double p1 = (double)variantSuccesses / variantTrials;
        double p2 = (double)controlSuccesses / controlTrials;
        double pooled = (double)(variantSuccesses + controlSuccesses) / (variantTrials + controlTrials);

        double seSquared = pooled * (1.0 - pooled) * (1.0 / variantTrials + 1.0 / controlTrials);
        if (seSquared <= 0.0)
        {
            return 1.0;
        }

        double z = (p1 - p2) / Math.Sqrt(seSquared);
        // Two-tailed p-value.
        return 2.0 * (1.0 - StandardNormalCdf(Math.Abs(z)));
    }

    private static (double Low, double High) AbsoluteDifferenceConfidenceInterval(
        double variantRate, long variantTrials, double controlRate, long controlTrials)
    {
        if (variantTrials <= 0 || controlTrials <= 0)
        {
            return (0.0, 0.0);
        }

        double seSquared =
            (variantRate * (1.0 - variantRate) / variantTrials)
            + (controlRate * (1.0 - controlRate) / controlTrials);
        double se = Math.Sqrt(Math.Max(seSquared, 0.0));
        double diff = variantRate - controlRate;
        return (diff - ZCriticalNinetyFive * se, diff + ZCriticalNinetyFive * se);
    }

    // Abramowitz & Stegun 7.1.26 approximation for erf, used to compute Phi(z).
    private static double StandardNormalCdf(double z) => 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));

    private static double Erf(double x)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        double absX = Math.Abs(x);
        double t = 1.0 / (1.0 + p * absX);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-absX * absX);
        return sign * y;
    }
}
