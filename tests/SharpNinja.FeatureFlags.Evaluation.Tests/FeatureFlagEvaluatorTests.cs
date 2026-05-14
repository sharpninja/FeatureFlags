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

    /// <summary>FR-5 TR-2 verifies the v1 CEL subset over nested context fields.</summary>
    [Fact]
    public void EvaluateSupportsTrimSafeCelSubset()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set(
                "user",
                new Dictionary<string, object?>
                {
                    ["region"] = "us",
                    ["role"] = "dispatcher",
                    ["roles"] = AdminRoles,
                    ["first"] = "Ada",
                    ["last"] = "Lovelace",
                })
            .Set("score", 10)
            .Set("SemanticVersion", "2.3.4")
            .Build();

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "complex-rule", false, context);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-5 verifies semantic-version comparison helpers.</summary>
    [Fact]
    public void EvaluateSupportsSemverCompareFunction()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("SemanticVersion", "2.4.0")
            .Build();

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "semver-compare", false, context);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-4 TR-3 verifies deterministic percentage bucketing across repeated evaluations.</summary>
    [Fact]
    public void EvaluatePercentageBucketingIsDeterministicAcrossRepeatedInputs()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(ManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);

        for (int index = 0; index < 128; index++)
        {
            EvaluationContext context = EvaluationContext.Builder()
                .Set("UserId", string.Concat("user-", index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Build();

            EvaluationResult<string> first = evaluator.Evaluate(
                manifest,
                "truckmate",
                "rollout",
                "fallback",
                context);

            for (int repeat = 0; repeat < 5; repeat++)
            {
                EvaluationResult<string> next = evaluator.Evaluate(
                    manifest,
                    "truckmate",
                    "rollout",
                    "fallback",
                    context);

                Assert.Equal(first.Value, next.Value);
                Assert.Equal(first.Reason, next.Reason);
                Assert.Equal(first.RuleIndex, next.RuleIndex);
            }
        }
    }

    private static readonly string[] AdminRoles = ["driver", "admin"];

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
            },
            {
              "key": "complex-rule",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "  ( user.region == 'us' && score + 5 >= 15 && user.role in ['driver', 'dispatcher'] && user.roles.exists(role, role == 'admin') && semver_satisfies(SemanticVersion, '>=2.3.0') && user.first + '-' + user.last == 'Ada-Lovelace' )  ",
                  "value": true
                }
              ]
            },
            {
              "key": "semver-compare",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "semver_compare(SemanticVersion, '2.3.0') >= 0",
                  "value": true
                }
              ]
            },
            {
              "key": "rollout",
              "type": "string",
              "defaultValue": "off",
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "bucket(UserId, 50)",
                  "value": "on"
                }
              ]
            }
          ]
        }
        """;
}
