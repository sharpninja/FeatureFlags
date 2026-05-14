using SharpNinja.FeatureFlags.Abstractions;

namespace SharpNinja.FeatureFlags.Evaluation;

internal static class RulePredicateMatcher
{
    public static RulePredicateProgram Compile(string predicate) =>
        RulePredicateCompiler.Compile(predicate);

    public static bool IsMatch(
        FeatureFlagRule rule,
        EvaluationContext context,
        string productId,
        string releaseId,
        string flagKey)
    {
        RulePredicateProgram program = rule.CompiledPredicate ?? Compile(rule.When);
        return program.Evaluate(context, productId, releaseId, flagKey);
    }
}
