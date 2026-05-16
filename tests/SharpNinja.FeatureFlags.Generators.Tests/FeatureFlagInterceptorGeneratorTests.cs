using System.Linq;
using Xunit;

namespace SharpNinja.FeatureFlags.Generators.Tests;

/// <summary>
/// Unit tests for <see cref="FeatureFlagInterceptorGenerator"/>.
/// Verifies that C# 13 interceptors are emitted for call sites of methods decorated with
/// <c>[InterceptFeatureFlag]</c>, gating each call behind an
/// <c>ISharpNinjaFeatureClient.Evaluate(flagKey)</c> check.
/// </summary>
public sealed class FeatureFlagInterceptorGeneratorTests
{
    private const string AbstractionsUsings = """
        using SharpNinja.FeatureFlags.Abstractions;
        using SharpNinja.FeatureFlags.Abstractions.Attributes;
        """;

    /// <summary>
    /// Given a method <c>Foo(int)</c> marked <c>[InterceptFeatureFlag("foo.enabled")]</c>
    /// and a single call site <c>Foo(42)</c>, the generator must emit exactly one interceptor
    /// source file.
    /// </summary>
    [Fact]
    public void EmitsOneInterceptorFileForSingleCallSite()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public static class FooHost
            {
                [InterceptFeatureFlag("foo.enabled")]
                public static int Foo(int x) => x;
            }

            public static class CallerCode
            {
                public static int Run(ISharpNinjaFeatureClient client)
                {
                    return FooHost.Foo(42);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagInterceptorGenerator>(compilation);

        var sources = GeneratorTestHelper.GetAllGeneratedSources(result);
        var interceptorFiles = sources
            .Where(s => s.HintName.StartsWith("FeatureFlagInterceptors", System.StringComparison.Ordinal))
            .ToList();

        Assert.Single(interceptorFiles);
    }

    /// <summary>
    /// The emitted interceptor source must include the
    /// <c>[InterceptsLocation(version: 1, data: ...)]</c> attribute that wires the
    /// generated stub to the original call site.
    /// </summary>
    [Fact]
    public void EmittedInterceptorContainsInterceptsLocationAttribute()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public static class FooHost
            {
                [InterceptFeatureFlag("foo.enabled")]
                public static int Foo(int x) => x;
            }

            public static class CallerCode
            {
                public static int Run(ISharpNinjaFeatureClient client)
                {
                    return FooHost.Foo(42);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagInterceptorGenerator>(compilation);

        var sources = GeneratorTestHelper.GetAllGeneratedSources(result);
        var interceptor = sources.FirstOrDefault(s =>
            s.HintName.StartsWith("FeatureFlagInterceptors", System.StringComparison.Ordinal));

        Assert.NotEqual(default, interceptor);
        Assert.Contains("InterceptsLocation", interceptor.Source, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The emitted interceptor must evaluate the configured flag through
    /// <c>ISharpNinjaFeatureClient.Evaluate("foo.enabled", ...)</c> and throw
    /// <c>FeatureFlagDisabledException</c> when the result is <see langword="false"/>.
    /// </summary>
    [Fact]
    public void EmittedInterceptorEvaluatesFlagAndThrowsWhenDisabled()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public static class FooHost
            {
                [InterceptFeatureFlag("foo.enabled")]
                public static int Foo(int x) => x;
            }

            public static class CallerCode
            {
                public static int Run(ISharpNinjaFeatureClient client)
                {
                    return FooHost.Foo(42);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagInterceptorGenerator>(compilation);

        var sources = GeneratorTestHelper.GetAllGeneratedSources(result);
        var interceptor = sources.FirstOrDefault(s =>
            s.HintName.StartsWith("FeatureFlagInterceptors", System.StringComparison.Ordinal));

        Assert.NotEqual(default, interceptor);
        Assert.Contains("Evaluate(\"foo.enabled\"", interceptor.Source, System.StringComparison.Ordinal);
        Assert.Contains("FeatureFlagDisabledException", interceptor.Source, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// When no method is decorated with <c>[InterceptFeatureFlag]</c>, the generator must
    /// emit no interceptor source files.
    /// </summary>
    [Fact]
    public void EmitsNoInterceptorWhenAttributeAbsent()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public static class FooHost
            {
                public static int Foo(int x) => x;
            }

            public static class CallerCode
            {
                public static int Run()
                {
                    return FooHost.Foo(42);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagInterceptorGenerator>(compilation);

        var sources = GeneratorTestHelper.GetAllGeneratedSources(result);
        var interceptorFiles = sources
            .Where(s => s.HintName.StartsWith("FeatureFlagInterceptors", System.StringComparison.Ordinal))
            .ToList();

        Assert.Empty(interceptorFiles);
    }

    /// <summary>
    /// Each call site of a method decorated with <c>[InterceptFeatureFlag]</c> requires its
    /// own <c>[InterceptsLocation]</c> attribute. Two call sites must therefore produce two
    /// <c>InterceptsLocation</c> occurrences in the generated output.
    /// </summary>
    [Fact]
    public void EmitsOneInterceptorPerCallSite()
    {
        string source = AbstractionsUsings + """

            namespace MyApp;

            public static class FooHost
            {
                [InterceptFeatureFlag("foo.enabled")]
                public static int Foo(int x) => x;
            }

            public static class CallerCode
            {
                public static int RunFirst(ISharpNinjaFeatureClient client)
                {
                    return FooHost.Foo(1);
                }

                public static int RunSecond(ISharpNinjaFeatureClient client)
                {
                    return FooHost.Foo(2);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<FeatureFlagInterceptorGenerator>(compilation);

        var sources = GeneratorTestHelper.GetAllGeneratedSources(result);
        string combined = string.Concat(sources
            .Where(s => s.HintName.StartsWith("FeatureFlagInterceptors", System.StringComparison.Ordinal))
            .Select(s => s.Source));

        // Two call sites means two [InterceptsLocationAttribute(...)] usages on intercepting
        // methods. The polyfill declaration of the attribute itself does not count because
        // it is matched separately by its "public " constructor prefix.
        int occurrences = CountOccurrences(combined, "InterceptsLocationAttribute(1, \"");
        Assert.Equal(2, occurrences);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
