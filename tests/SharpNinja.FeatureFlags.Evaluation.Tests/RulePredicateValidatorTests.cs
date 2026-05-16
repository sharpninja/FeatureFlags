using Microsoft.Extensions.Logging.Abstractions;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Evaluation;
using Xunit;

namespace SharpNinja.FeatureFlags.Evaluation.Tests;

/// <summary>FR-5 TR-3 validator and decimal-coercion tests for the v1 CEL rule predicate subset.</summary>
public sealed class RulePredicateValidatorTests
{
    /// <summary>TR-3 verifies binary floating-point operands are coerced to System.Decimal so 1.1 + 2.2 == 3.3 holds.</summary>
    [Fact]
    public void EvaluateFloatComparisonHandlesIeee754AdditionThroughDecimalCoercion()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(NumericComparisonManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "decimal-equality", false);

        Assert.True(result.Value);
        Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    }

    /// <summary>TR-3 verifies a NaN context value is rejected by the numeric-coercion path and the rule does not match.</summary>
    [Fact]
    public void EvaluateNumericComparisonReturnsFalseWhenContextValueIsNaN()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(NumericComparisonManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("score", double.NaN)
            .Build();

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "score-threshold", false, context);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    /// <summary>TR-3 verifies a positive-infinity context value is rejected by the numeric-coercion path and the rule does not match.</summary>
    [Fact]
    public void EvaluateNumericComparisonReturnsFalseWhenContextValueIsPositiveInfinity()
    {
        FeatureFlagManifest manifest = FeatureFlagManifest.Parse(NumericComparisonManifestJson);
        var evaluator = new FeatureFlagEvaluator(NullLogger<FeatureFlagEvaluator>.Instance);
        EvaluationContext context = EvaluationContext.Builder()
            .Set("score", double.PositiveInfinity)
            .Build();

        EvaluationResult<bool> result = evaluator.Evaluate(manifest, "truckmate", "score-threshold", false, context);

        Assert.False(result.Value);
        Assert.Equal(EvaluationReason.Default, result.Reason);
    }

    /// <summary>FR-5 verifies predicates referencing user-defined function names are rejected at validate time.</summary>
    [Fact]
    public void ValidateRejectsPredicateReferencingUnknownFunctionName()
    {
        RulePredicateValidationResult result = RulePredicateValidator.Validate("myFn(x) == 1");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("myFn", StringComparison.Ordinal));
    }

    /// <summary>FR-5 verifies define-and-call syntax is not parseable as a v1 CEL predicate.</summary>
    [Fact]
    public void ValidateRejectsDefineAndCallSyntaxAsInvalidPredicate()
    {
        RulePredicateValidationResult result = RulePredicateValidator.Validate("define f(x) = x; f(1)");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
    }

    /// <summary>FR-5 verifies calling a function not in the supported subset fails validation rather than dispatching at evaluate time.</summary>
    [Fact]
    public void ValidateRejectsCallToFunctionOutsideSupportedSubset()
    {
        RulePredicateValidationResult result = RulePredicateValidator.Validate("custom_helper('a', 'b') == true");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("custom_helper", StringComparison.Ordinal));
    }

    private const string NumericComparisonManifestJson = """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05.14",
          "environment": "Development",
          "flags": [
            {
              "key": "decimal-equality",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "1.1 + 2.2 == 3.3",
                  "value": true
                }
              ]
            },
            {
              "key": "score-threshold",
              "type": "boolean",
              "defaultValue": false,
              "killable": false,
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "score > 0.0",
                  "value": true
                }
              ]
            }
          ]
        }
        """;
}
