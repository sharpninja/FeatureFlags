using SharpNinja.FeatureFlags.Samples.Avalonia12;
using Xunit;

namespace SharpNinja.FeatureFlags.Avalonia12.IntegrationTests;

/// <summary>Integration tests for the Avalonia 12 sample scenario runner.</summary>
public sealed class AvaloniaSampleScenarioRunnerTests
{
    private static readonly string[] ExpectedProjectIds = ["alpha", "beta"];
    private static readonly string[] ExpectedFeatureKeys = ["dashboard.enabled", "reports.title"];
    private static readonly (string ProjectId, string FeatureKey)[] ExpectedOrder =
    [
        ("alpha", "dashboard.enabled"),
        ("alpha", "reports.title"),
        ("beta", "dashboard.enabled"),
        ("beta", "reports.title"),
    ];

    /// <summary>Verifies that the sample covers the required Projects and Features matrix.</summary>
    [Fact]
    public void GetOutputsCoversTwoProjectsAndTwoFeaturesWithAllCombinations()
    {
        IReadOnlyList<AvaloniaSampleScenarioOutput> outputs = AvaloniaSampleScenarioRunner.GetOutputs();

        Assert.Equal(ExpectedProjectIds, outputs.Select(output => output.ProjectId).Distinct().Order());
        Assert.Equal(ExpectedFeatureKeys, outputs.Select(output => output.FeatureKey).Distinct().Order());
        Assert.Equal(ExpectedOrder, outputs.Select(output => (output.ProjectId, output.FeatureKey)));
        Assert.Equal(4, outputs.Count);
    }

    /// <summary>Verifies actual scenario output fields against the expected snapshot contract.</summary>
    [Fact]
    public void GetOutputsMatchesExpectedSnapshots()
    {
        IReadOnlyList<AvaloniaSampleScenarioOutput> actual = AvaloniaSampleScenarioRunner.GetOutputs();
        IReadOnlyList<AvaloniaSampleScenarioOutput> expected = AvaloniaSampleScenarioRunner.GetExpectedOutputs();

        Assert.Equal(expected, actual);
    }

    /// <summary>Verifies beta reports are product-scoped away from the rule and use the default fallback.</summary>
    [Fact]
    public void GetOutputsBetaReportsTitleUsesDefaultFallback()
    {
        AvaloniaSampleScenarioOutput output = Assert.Single(
            AvaloniaSampleScenarioRunner.GetOutputs(),
            scenario => scenario.ProjectId == "beta" && scenario.FeatureKey == "reports.title");

        Assert.Equal("Reports default fallback", output.ResolvedValue);
        Assert.Equal("Default", output.Reason);
        Assert.Contains("Reports default fallback", output.DisplayText, StringComparison.Ordinal);
        Assert.Contains("(Default)", output.DisplayText, StringComparison.Ordinal);
    }
}
