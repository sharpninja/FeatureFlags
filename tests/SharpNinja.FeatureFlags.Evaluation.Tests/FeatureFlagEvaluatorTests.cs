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

    /// <summary>FR-5 verifies CEL ternary operator with a boolean condition returning a string result.</summary>
    [Fact]
    public void EvaluateSupportsTernaryOperatorWithBooleanCondition()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(TernaryManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("score", 75)
            .Build();

        EvaluationResult<string> result = evaluator.Evaluate(manifest, "truckmate", "ternary-basic", "fallback", context);

        Assert.Equal("high", result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-5 verifies CEL ternary operator with false condition selects the else branch.</summary>
    [Fact]
    public void EvaluateSupportsTernaryOperatorFalseBranchSelection()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(TernaryManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("score", 30)
            .Build();

        EvaluationResult<string> result = evaluator.Evaluate(manifest, "truckmate", "ternary-basic", "fallback", context);

        Assert.Equal("low", result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-5 verifies CEL ternary operator used inside a boolean comparison (nested ternary).</summary>
    [Fact]
    public void EvaluateSupportsTernaryOperatorNestedInBooleanComparison()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(TernaryManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("score", 90)
            .Set("tier", "gold")
            .Build();

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "ternary-nested", false, context);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-5 verifies the CEL filter macro returns elements that satisfy the predicate.</summary>
    [Fact]
    public void EvaluateSupportsFilterMacroReturningMatchingElements()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(MacroManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("scores", new List<object?> { 10, 60, 80, 40, 90 })
            .Build();

        // filter(scores, s, s > 50) has 3 elements (60, 80, 90); exists checks .exists(s, s > 50) => true
        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "filter-macro", false, context);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-5 verifies the CEL filter macro with no matching elements produces an empty list.</summary>
    [Fact]
    public void EvaluateSupportsFilterMacroWithNoMatchingElementsProducesEmptyList()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(MacroManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("scores", new List<object?> { 10, 20, 30 })
            .Build();

        // filter(scores, s, s > 50) is empty; exists checks .exists(s, s > 50) => false
        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "filter-macro", false, context);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    /// <summary>FR-5 verifies the CEL map macro transforms each element and the result is usable.</summary>
    [Fact]
    public void EvaluateSupportsMapMacroTransformingElements()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(MacroManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("tags", new List<object?> { "admin", "viewer", "editor" })
            .Build();

        // map(tags, t, t) chains with exists(t, t == 'admin') => true
        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "map-macro", false, context);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>FR-5 verifies the CEL map macro with no matching value in mapped list.</summary>
    [Fact]
    public void EvaluateSupportsMapMacroWithNoMatchInMappedList()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(MacroManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("tags", new List<object?> { "viewer", "editor" })
            .Build();

        // map(tags, t, t).exists(t, t == 'admin') => false
        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "map-macro", false, context);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    private static readonly string[] AdminRoles = ["driver", "admin"];

    private const string TernaryManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05.14",
          "environment": "Development",
          "flags": [
            {
              "key": "ternary-basic",
              "type": "string",
              "defaultValue": "none",
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "(score >= 50 ? 'high' : 'low') == 'high'",
                  "value": "high"
                },
                {
                  "when": "(score >= 50 ? 'high' : 'low') == 'low'",
                  "value": "low"
                }
              ]
            },
            {
              "key": "ternary-nested",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "(score >= 80 ? (tier == 'gold' ? true : false) : false) == true",
                  "value": true
                }
              ]
            }
          ]
        }
        """;

    private const string MacroManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05.14",
          "environment": "Development",
          "flags": [
            {
              "key": "filter-macro",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "scores.filter(s, s > 50).exists(s, s > 50)",
                  "value": true
                }
              ]
            },
            {
              "key": "map-macro",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "tags.map(t, t).exists(t, t == 'admin')",
                  "value": true
                }
              ]
            }
          ]
        }
        """;

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
