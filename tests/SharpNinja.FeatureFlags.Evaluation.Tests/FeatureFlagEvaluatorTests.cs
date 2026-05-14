using Microsoft.Extensions.Logging.Abstractions;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Evaluation;
using Xunit;

namespace SharpNinja.FeatureFlags.Evaluation.Tests;

/// <summary>TR-11 tests for manifest-backed feature flag evaluation.</summary>
public sealed class FeatureFlagEvaluatorTests
{
    /// <summary>TR-11 verifies manifest default resolution.</summary>
    [Fact]
    public void EvaluateReturnsManifestDefaultWhenFlagExistsAndNoRuleMatches()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "new-dashboard", false);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>TR-11 verifies first-match deterministic rule resolution.</summary>
    [Fact]
    public void EvaluateReturnsFirstMatchingRuleValue()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("project", "alpha")
            .Build();

        EvaluationResult<string> result = evaluator.Evaluate(manifest, "truckmate", "theme", "classic", context);

        Assert.Equal("modern", result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>TR-11 verifies product-scope denial falls back without throwing.</summary>
    [Fact]
    public void EvaluateReturnsCallerDefaultWhenProductScopeDeniesProduct()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "dispatch", "new-dashboard", false);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
        Assert.Contains("productScope", result.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>TR-11 verifies missing flags fall back without throwing.</summary>
    [Fact]
    public void EvaluateReturnsCallerDefaultWhenFlagIsMissing()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);

        EvaluationResult<int> result = evaluator.Evaluate(manifest, "truckmate", "missing", 42);

        Assert.Equal(42, result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>TR-11 verifies requested type mismatches return an error fallback.</summary>
    [Fact]
    public void EvaluateReturnsCallerDefaultWithErrorWhenRequestedTypeMismatchesManifestType()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);

        EvaluationResult<string> result = evaluator.Evaluate(manifest, "truckmate", "new-dashboard", "fallback");

        Assert.Equal("fallback", result.Value);
        Assert.Equal(EvaluationReason.Error, result.Reason);
        Assert.Contains("incompatible", result.ErrorMessage, StringComparison.Ordinal);
    }

    private const string ManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05.14",
          "environment": "Development",
          "flags": [
            {
              "key": "new-dashboard",
              "type": "boolean",
              "defaultValue": true,
              "killable": true,
              "productScope": [ "truckmate" ]
            },
            {
              "key": "theme",
              "type": "string",
              "defaultValue": "classic",
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "project == 'alpha'",
                  "value": "modern"
                },
                {
                  "when": "user.region == \"us\"",
                  "value": "regional"
                }
              ]
            }
          ]
        }
        """;
}
