using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace SharpNinja.FeatureFlags.Generators.Tests;

/// <summary>
/// Unit tests for <see cref="ProductScopeGenerator"/>.
/// Verifies that the generator emits the correct <c>ProductScopeConstants</c> class
/// when <c>[assembly: ProductScope("...")]</c> is present, and emits nothing when absent.
/// </summary>
public sealed class ProductScopeGeneratorTests
{
    /// <summary>
    /// When the compiling assembly has <c>[assembly: ProductScope("truckmate")]</c>,
    /// the generator must emit a <c>ProductScopeConstants</c> class with
    /// <c>ProductId = "truckmate"</c>.
    /// </summary>
    [Fact]
    public void EmitsProductScopeConstantsClassWhenAttributePresent()
    {
        const string source = """
            using SharpNinja.FeatureFlags.Abstractions;
            [assembly: ProductScope("truckmate")]
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<ProductScopeGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(result, "ProductScopeConstants.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("class ProductScopeConstants", generated, StringComparison.Ordinal);
        Assert.Contains("public const string ProductId", generated, StringComparison.Ordinal);
        Assert.Contains("\"truckmate\"", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The emitted <c>ProductScopeConstants.ProductId</c> constant must exactly match
    /// the value passed to <c>[assembly: ProductScope]</c>.
    /// </summary>
    [Fact]
    public void EmittedProductIdMatchesAttributeValue()
    {
        const string productId = "my-custom-product";
        string source = $"""
            using SharpNinja.FeatureFlags.Abstractions;
            [assembly: ProductScope("{productId}")]
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<ProductScopeGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(result, "ProductScopeConstants.g.cs");

        Assert.NotNull(generated);
        Assert.Contains($"\"{productId}\"", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// When no <c>[assembly: ProductScope]</c> attribute is present, the generator must
    /// produce no output and no diagnostics.
    /// </summary>
    [Fact]
    public void EmitsNothingWhenAttributeAbsent()
    {
        const string source = """
            // No ProductScope attribute applied.
            namespace MyApp { }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<ProductScopeGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(result, "ProductScopeConstants.g.cs");

        Assert.Null(generated);

        // No diagnostics should be emitted when the attribute is absent.
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    /// The generator must produce no diagnostics when a valid <c>[assembly: ProductScope]</c>
    /// attribute is present.
    /// </summary>
    [Fact]
    public void ProducesNoDiagnosticsWhenAttributePresent()
    {
        const string source = """
            using SharpNinja.FeatureFlags.Abstractions;
            [assembly: ProductScope("truckmate")]
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<ProductScopeGenerator>(compilation);

        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    /// The emitted class must be declared as <c>internal static</c> (not public)
    /// so it does not leak from the consuming assembly.
    /// </summary>
    [Fact]
    public void EmittedClassIsInternalStatic()
    {
        const string source = """
            using SharpNinja.FeatureFlags.Abstractions;
            [assembly: ProductScope("truckmate")]
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator<ProductScopeGenerator>(compilation);

        string? generated = GeneratorTestHelper.GetGeneratedSource(result, "ProductScopeConstants.g.cs");

        Assert.NotNull(generated);
        Assert.Contains("internal static class ProductScopeConstants", generated, StringComparison.Ordinal);
    }
}
