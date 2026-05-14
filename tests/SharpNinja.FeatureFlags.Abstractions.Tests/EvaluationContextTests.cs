using SharpNinja.FeatureFlags.Abstractions;
using Xunit;

namespace SharpNinja.FeatureFlags.Abstractions.Tests;

/// <summary>Phase 0 contract tests for evaluation context signatures.</summary>
public sealed class EvaluationContextTests
{
    /// <summary>The builder creates an immutable context with supplied values.</summary>
    [Fact]
    public void BuilderCreatesContext()
    {
        var context = EvaluationContext.Builder()
            .Set("region", "us")
            .Build();

        Assert.Equal("us", context.Values["region"]);
    }
}
