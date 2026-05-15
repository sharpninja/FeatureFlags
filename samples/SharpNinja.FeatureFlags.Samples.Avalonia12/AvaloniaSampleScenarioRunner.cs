using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>Runs TEST-AVALONIA-SAMPLE-001 Avalonia 12 feature flag scenarios for tests and UI rendering.</summary>
public static class AvaloniaSampleScenarioRunner
{
    private const string DashboardEnabledFeatureKey = "dashboard.enabled";
    private const string ReportsTitleFeatureKey = "reports.title";
    private const string TruckMateProjectId = SharpNinjaProductCatalog.TruckMate;
    private const string DriverMateProjectId = SharpNinjaProductCatalog.DriverMate;
    private const string DashboardDefaultText = "Dashboard default fallback";
    private const string ReportsDefaultTitle = "Reports default fallback";
    private static readonly string[] ProjectIds = [TruckMateProjectId, DriverMateProjectId];

    private static string CreateManifestJson(string productId) =>
        $$"""
        {
          "schemaVersion": 1,
          "productId": "{{productId}}",
          "releaseId": "avalonia12-sample",
          "environment": "Development",
          "flags": [
            {
              "key": "dashboard.enabled",
              "type": "boolean",
              "defaultValue": false,
              "killable": true,
              "productScope": [ "{{productId}}" ],
              "rules": [
                {
                  "when": "project.id == 'truckmate'",
                  "value": true
                }
              ]
            },
            {
              "key": "reports.title",
              "type": "string",
              "defaultValue": "Reports default fallback",
              "killable": false,
              "productScope": [ "{{productId}}" ],
              "rules": [
                {
                  "when": "project.id == 'truckmate'",
                  "value": "TruckMate Reports"
                }
              ]
            }
          ]
        }
        """;

    /// <summary>Gets the TEST-AVALONIA-SAMPLE-001 outputs for all project and feature scenarios.</summary>
    /// <returns>All four scenario outputs in project then feature order.</returns>
    public static IReadOnlyList<AvaloniaSampleScenarioOutput> GetOutputs()
    {
        List<AvaloniaSampleScenarioOutput> outputs = [];

        foreach (string projectId in ProjectIds)
        {
            using ServiceProvider provider = CreateProvider(projectId);
            ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();
            EvaluationContext context = CreateContext(projectId);

            EvaluationResult<bool> dashboard = client.Evaluate(
                DashboardEnabledFeatureKey,
                defaultValue: false,
                context);
            outputs.Add(ToOutput(
                projectId,
                DashboardEnabledFeatureKey,
                displayLabel: "Dashboard enabled",
                resolvedValue: dashboard.Value ? "enabled" : DashboardDefaultText,
                dashboard.Reason));

            EvaluationResult<string> reports = client.Evaluate(
                ReportsTitleFeatureKey,
                ReportsDefaultTitle,
                context);
            outputs.Add(ToOutput(
                projectId,
                ReportsTitleFeatureKey,
                displayLabel: "Reports title",
                resolvedValue: reports.Value,
                reports.Reason));
        }

        return outputs;
    }

    /// <summary>Gets the expected TEST-AVALONIA-SAMPLE-001 output snapshots for integration tests.</summary>
    /// <returns>All four expected outputs in project then feature order.</returns>
    public static IReadOnlyList<AvaloniaSampleScenarioOutput> GetExpectedOutputs() =>
    [
        new(
            TruckMateProjectId,
            DashboardEnabledFeatureKey,
            "truckmate dashboard.enabled Dashboard enabled: enabled (RuleMatch)",
            "enabled",
            nameof(EvaluationReason.RuleMatch)),
        new(
            TruckMateProjectId,
            ReportsTitleFeatureKey,
            "truckmate reports.title Reports title: TruckMate Reports (RuleMatch)",
            "TruckMate Reports",
            nameof(EvaluationReason.RuleMatch)),
        new(
            DriverMateProjectId,
            DashboardEnabledFeatureKey,
            string.Concat(
                "drivermate dashboard.enabled Dashboard enabled: ",
                DashboardDefaultText,
                " (Default)"),
            DashboardDefaultText,
            nameof(EvaluationReason.Default)),
        new(
            DriverMateProjectId,
            ReportsTitleFeatureKey,
            string.Concat(
                "drivermate reports.title Reports title: ",
                ReportsDefaultTitle,
                " (Default)"),
            ReportsDefaultTitle,
            nameof(EvaluationReason.Default)),
    ];

    private static ServiceProvider CreateProvider(string productId)
    {
        ServiceCollection services = new();
        services.AddSharpNinjaFeatureFlags(
            new SharpNinjaFeatureFlagOptions(
                productId,
                "avalonia12-sample",
                "Development",
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)),
            CreateManifestJson(productId));

        return services.BuildServiceProvider();
    }

    private static EvaluationContext CreateContext(string projectId) =>
        EvaluationContext.Builder()
            .Set("project.id", projectId)
            .Build();

    private static AvaloniaSampleScenarioOutput ToOutput(
        string projectId,
        string featureKey,
        string displayLabel,
        string resolvedValue,
        EvaluationReason reason)
    {
        string reasonText = reason.ToString();
        return new AvaloniaSampleScenarioOutput(
            projectId,
            featureKey,
            string.Concat(projectId, " ", featureKey, " ", displayLabel, ": ", resolvedValue, " (", reasonText, ")"),
            resolvedValue,
            reasonText);
    }
}

/// <summary>Stable TEST-AVALONIA-SAMPLE-001 output contract for Avalonia 12 sample integration tests.</summary>
/// <param name="ProjectId">Project identifier evaluated by the scenario.</param>
/// <param name="FeatureKey">Feature flag key evaluated by the scenario.</param>
/// <param name="DisplayText">Visible text rendered by the sample UI.</param>
/// <param name="ResolvedValue">Resolved value normalized for snapshot assertions.</param>
/// <param name="Reason">Evaluation reason name.</param>
public sealed record AvaloniaSampleScenarioOutput(
    string ProjectId,
    string FeatureKey,
    string DisplayText,
    string ResolvedValue,
    string Reason);
