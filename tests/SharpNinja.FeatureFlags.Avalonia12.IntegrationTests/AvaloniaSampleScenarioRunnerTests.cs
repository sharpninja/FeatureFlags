using SharpNinja.FeatureFlags.Abstractions.Options;
using SharpNinja.FeatureFlags.Samples.Avalonia12;
using Xunit;

namespace SharpNinja.FeatureFlags.Avalonia12.IntegrationTests;

/// <summary>FR-4 TR-2 TR-3 TR-5 TR-11 TEST-AVALONIA-SAMPLE-001 integration tests for the Avalonia 12 sample scenario runner.</summary>
public sealed class AvaloniaSampleScenarioRunnerTests
{
    private static readonly string[] ExpectedProjectIds =
    [
        SharpNinjaProductCatalog.DriverMate,
        SharpNinjaProductCatalog.TruckMate,
    ];

    private static readonly string[] ExpectedFeatureKeys = ["dashboard.enabled", "reports.title"];
    private static readonly (string ProjectId, string FeatureKey)[] ExpectedOrder =
    [
        (SharpNinjaProductCatalog.TruckMate, "dashboard.enabled"),
        (SharpNinjaProductCatalog.TruckMate, "reports.title"),
        (SharpNinjaProductCatalog.DriverMate, "dashboard.enabled"),
        (SharpNinjaProductCatalog.DriverMate, "reports.title"),
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

    /// <summary>Verifies DriverMate reports do not match the TruckMate project rule and use the default fallback.</summary>
    [Fact]
    public void GetOutputsDriverMateReportsTitleUsesDefaultFallback()
    {
        AvaloniaSampleScenarioOutput output = Assert.Single(
            AvaloniaSampleScenarioRunner.GetOutputs(),
            scenario => scenario.ProjectId == SharpNinjaProductCatalog.DriverMate && scenario.FeatureKey == "reports.title");

        Assert.Equal("Reports default fallback", output.ResolvedValue);
        Assert.Equal("Default", output.Reason);
        Assert.Contains("Reports default fallback", output.DisplayText, StringComparison.Ordinal);
        Assert.Contains("(Default)", output.DisplayText, StringComparison.Ordinal);
    }
}
