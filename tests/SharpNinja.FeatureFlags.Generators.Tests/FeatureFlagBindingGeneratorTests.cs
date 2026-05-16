using Xunit;

namespace SharpNinja.FeatureFlags.Generators.Tests;

/// <summary>
/// Unit tests for <see cref="FeatureFlagBindingGenerator"/>.
/// Verifies property getter generation for <c>[FeatureFlag]</c>-decorated partial properties,
/// and gate guard generation for <c>[FeatureFlagGate]</c>-decorated partial methods.
/// </summary>
public sealed class FeatureFlagBindingGeneratorTests
{
    private const string AbstractionsUsings = """
        using SharpNinja.FeatureFlags.Abstractions;
        using SharpNinja.FeatureFlags.Abstractions.Attributes;
        """;

    /// <summary>
    /// A partial property decorated with <c>[FeatureFlag("dashboard.enabled")]</c>
    /// must result in a generated getter that calls
    /// <c>_featureClient.Evaluate("dashboard.enabled", false, _featureFlagContext).Value</c>.
    /// </summary>
    [Fact]
    public void EmitsPropertyGetterForFeatureFlagDecoratedPartialProperty()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public partial class MyService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;

                [FeatureFlag("dashboard.enabled")]
                public partial bool DashboardEnabled { get; }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(
            result, "MyService_FeatureFlags_Properties.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("partial bool DashboardEnabled =>", generated, StringComparison.Ordinal);
        Assert.Contains("_featureClient.Evaluate(\"dashboard.enabled\"", generated, StringComparison.Ordinal);
        Assert.Contains("_featureFlagContext", generated, StringComparison.Ordinal);
        Assert.Contains(".Value", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A partial property with <c>[FeatureFlag]</c> must use the default value for
    /// its type (<c>false</c> for <c>bool</c>) when no <c>[FeatureFlagFallback]</c>
    /// attribute is present.
    /// </summary>
    [Fact]
    public void EmittedPropertyGetterUsesBoolDefaultWhenNoFallback()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public partial class MyService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;

                [FeatureFlag("my.flag")]
                public partial bool MyFlag { get; }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(
            result, "MyService_FeatureFlags_Properties.g.cs");

        Assert.NotNull(generated);
        // The bool default value is 'false'.
        Assert.Contains(", false,", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A partial method decorated with <c>[FeatureFlagGate("reports.view",
    /// DisabledBehavior = DisabledBehavior.Skip)]</c> must generate a method body
    /// that returns early (skips) when the flag evaluates to false.
    /// </summary>
    [Fact]
    public void EmitsSkipGateBodyForFeatureFlagGateWithSkipBehavior()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public partial class ReportService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;

                [FeatureFlagGate("reports.view", DisabledBehavior = DisabledBehavior.Skip)]
                public partial void ViewReports();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(
            result, "ReportService_FeatureFlags_Gates.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("partial void ViewReports()", generated, StringComparison.Ordinal);
        Assert.Contains("_featureClient.Evaluate(\"reports.view\"", generated, StringComparison.Ordinal);
        Assert.Contains("return;", generated, StringComparison.Ordinal);
        // Must NOT throw.
        Assert.DoesNotContain("FeatureFlagDisabledException", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A partial method decorated with <c>[FeatureFlagGate("reports.view",
    /// DisabledBehavior = DisabledBehavior.Throw)]</c> must generate a method body
    /// that throws <c>FeatureFlagDisabledException</c> when the flag evaluates to false.
    /// </summary>
    [Fact]
    public void EmitsThrowGateBodyForFeatureFlagGateWithThrowBehavior()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public partial class ReportService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;

                [FeatureFlagGate("reports.view", DisabledBehavior = DisabledBehavior.Throw)]
                public partial void ViewReports();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(
            result, "ReportService_FeatureFlags_Gates.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("partial void ViewReports()", generated, StringComparison.Ordinal);
        Assert.Contains("FeatureFlagDisabledException", generated, StringComparison.Ordinal);
        Assert.Contains("\"reports.view\"", generated, StringComparison.Ordinal);
        // Must NOT have a bare return when Throw is specified.
        Assert.DoesNotContain("\n            return;", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// A partial method decorated with <c>[FeatureFlagGate]</c> and
    /// <c>DisabledBehavior.ReturnFallback</c> (the default, value 0) must generate
    /// a guard that returns early without throwing.
    /// </summary>
    [Fact]
    public void EmitsReturnFallbackGateBodyForDefaultDisabledBehavior()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public partial class ReportService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;

                [FeatureFlagGate("reports.view")]
                public partial void ViewReports();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(
            result, "ReportService_FeatureFlags_Gates.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("return;", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("FeatureFlagDisabledException", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The generator must report a <c>SNFF001</c> diagnostic (error) when a member
    /// decorated with <c>[FeatureFlag]</c> is contained in a non-partial class.
    /// </summary>
    [Fact]
    public void ReportsDiagnosticWhenContainingClassIsNotPartial()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public class NonPartialService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;

                [FeatureFlag("my.flag")]
                public partial bool MyFlag { get; }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        // Expect at least one SNFF001 diagnostic.
        bool hasSNFF001 = result.Diagnostics.Any(d =>
            d.Id == "SNFF001");

        Assert.True(hasSNFF001, "Expected SNFF001 diagnostic for non-partial class.");
    }

    /// <summary>
    /// When a partial property has <c>[FeatureFlagFallback("_defaultDashboardEnabled")]</c>,
    /// the generated getter must use the fallback member as the default value argument.
    /// </summary>
    [Fact]
    public void EmittedPropertyGetterUsesFallbackMemberWhenFallbackAttributePresent()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public partial class MyService
            {
                private readonly ISharpNinjaFeatureClient _featureClient = null!;
                private readonly EvaluationContext? _featureFlagContext = null;
                private readonly bool _defaultDashboardEnabled = true;

                [FeatureFlag("dashboard.enabled")]
                [FeatureFlagFallback("_defaultDashboardEnabled")]
                public partial bool DashboardEnabled { get; }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagBindingGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(
            result, "MyService_FeatureFlags_Properties.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("_defaultDashboardEnabled", generated, StringComparison.Ordinal);
    }
}
