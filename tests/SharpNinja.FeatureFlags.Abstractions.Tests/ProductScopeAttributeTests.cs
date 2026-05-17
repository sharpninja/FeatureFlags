using SharpNinja.FeatureFlags.Abstractions;
using Xunit;

namespace SharpNinja.FeatureFlags.Abstractions.Tests;

/// <summary>
/// FR-1 FR-10 TR-1 tests for the v1.0.3-widened ProductScopeAttribute.
/// </summary>
/// <remarks>
/// v1.0.3 widens the v1.0.2 contract: AttributeTargets covers Class + Method + Assembly,
/// the constructor accepts params string[] products, and the type lives in the canonical
/// SharpNinja.FeatureFlags.Abstractions namespace.
/// </remarks>
public sealed class ProductScopeAttributeTests
{
    /// <summary>FR-1 single-arg ctor stores the trimmed product identifier in Products[0].</summary>
    [Fact]
    public void ConstructorStoresSingleProductId()
    {
        var attr = new ProductScopeAttribute("truckmate");

        Assert.Single(attr.Products);
        Assert.Equal("truckmate", attr.Products[0]);
        Assert.Equal("truckmate", attr.ProductId);
    }

    /// <summary>FR-1 single-arg ctor trims surrounding whitespace.</summary>
    [Fact]
    public void ConstructorTrimsSurroundingWhitespace()
    {
        var attr = new ProductScopeAttribute("  truckmate  ");

        Assert.Equal("truckmate", attr.ProductId);
    }

    /// <summary>FR-1 multi-product ctor stores all trimmed ids in order.</summary>
    [Fact]
    public void ConstructorStoresMultipleProducts()
    {
        var attr = new ProductScopeAttribute("truckmate", "drivermate");

        Assert.Equal(2, attr.Products.Count);
        Assert.Equal("truckmate", attr.Products[0]);
        Assert.Equal("drivermate", attr.Products[1]);
    }

    /// <summary>FR-1 multi-product ctor trims each individual id.</summary>
    [Fact]
    public void ConstructorTrimsEachProduct()
    {
        var attr = new ProductScopeAttribute("  truckmate  ", "  drivermate  ");

        Assert.Equal("truckmate", attr.Products[0]);
        Assert.Equal("drivermate", attr.Products[1]);
    }

    /// <summary>FR-1 ProductId accessor reads the first element.</summary>
    [Fact]
    public void ProductIdReturnsFirstElement()
    {
        var attr = new ProductScopeAttribute("truckmate", "drivermate");

        Assert.Equal("truckmate", attr.ProductId);
    }

    /// <summary>FR-1 Null params array throws ArgumentNullException.</summary>
    [Fact]
    public void ConstructorRejectsNullArray()
    {
        Assert.Throws<ArgumentNullException>(() => new ProductScopeAttribute(null!));
    }

    /// <summary>FR-1 Empty product identifier throws ArgumentException.</summary>
    [Fact]
    public void ConstructorRejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new ProductScopeAttribute(""));
    }

    /// <summary>FR-1 Whitespace-only product identifier throws ArgumentException.</summary>
    [Fact]
    public void ConstructorRejectsWhitespaceOnlyId()
    {
        Assert.Throws<ArgumentException>(() => new ProductScopeAttribute("   "));
    }

    /// <summary>FR-1 Any whitespace-only id in a multi-element call throws ArgumentException.</summary>
    [Fact]
    public void ConstructorRejectsWhitespaceMidArray()
    {
        Assert.Throws<ArgumentException>(() => new ProductScopeAttribute("truckmate", "   "));
    }

    /// <summary>FR-1 Empty params array is permitted and yields no-restriction semantics.</summary>
    [Fact]
    public void EmptyArrayProducesNoRestriction()
    {
        var attr = new ProductScopeAttribute();

        Assert.Empty(attr.Products);
        Assert.True(attr.MatchesProduct("truckmate"));
        Assert.True(attr.MatchesProduct("drivermate"));
        Assert.True(attr.MatchesProduct(null));
    }

    /// <summary>FR-1 MatchesProduct returns true when productId is in Products.</summary>
    [Fact]
    public void MatchesProductReturnsTrueForListedId()
    {
        var attr = new ProductScopeAttribute("truckmate", "drivermate");

        Assert.True(attr.MatchesProduct("truckmate"));
        Assert.True(attr.MatchesProduct("drivermate"));
    }

    /// <summary>FR-1 MatchesProduct returns false when productId is not listed.</summary>
    [Fact]
    public void MatchesProductReturnsFalseForUnlistedId()
    {
        var attr = new ProductScopeAttribute("truckmate");

        Assert.False(attr.MatchesProduct("drivermate"));
        Assert.False(attr.MatchesProduct("unknown"));
    }

    /// <summary>FR-1 MatchesProduct returns false for null / empty when scope is non-empty.</summary>
    [Fact]
    public void MatchesProductReturnsFalseForNullOrEmptyWhenScoped()
    {
        var attr = new ProductScopeAttribute("truckmate");

        Assert.False(attr.MatchesProduct(null));
        Assert.False(attr.MatchesProduct(""));
    }

    /// <summary>FR-1 ProductId throws when no products declared.</summary>
    [Fact]
    public void ProductIdThrowsWhenNoProducts()
    {
        var attr = new ProductScopeAttribute();

        Assert.Throws<InvalidOperationException>(() => _ = attr.ProductId);
    }

    /// <summary>FR-1 Attribute targets Class + Method + Assembly per v1.0.3 widen.</summary>
    [Fact]
    public void AttributeTargetsClassMethodAndAssembly()
    {
        var usage = (AttributeUsageAttribute)typeof(ProductScopeAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        const AttributeTargets expected = AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Assembly;
        Assert.Equal(expected, usage.ValidOn);
    }

    /// <summary>FR-1 Attribute does not allow multiple declarations on the same target.</summary>
    [Fact]
    public void AttributeDoesNotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute)typeof(ProductScopeAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        Assert.False(usage.AllowMultiple);
    }
}
