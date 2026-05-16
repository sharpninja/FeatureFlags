using SharpNinja.FeatureFlags.Abstractions.Attributes;
using Xunit;

namespace SharpNinja.FeatureFlags.Abstractions.Tests;

/// <summary>FR-1 FR-10 TR-1 tests for the assembly-level ProductScopeAttribute.</summary>
public sealed class ProductScopeAttributeTests
{
    /// <summary>FR-1 Attribute stores the trimmed product identifier.</summary>
    [Fact]
    public void ConstructorStoresProductId()
    {
        var attr = new ProductScopeAttribute("truckmate");

        Assert.Equal("truckmate", attr.ProductId);
    }

    /// <summary>FR-1 Attribute trims surrounding whitespace from the product identifier.</summary>
    [Fact]
    public void ConstructorTrimsSurroundingWhitespace()
    {
        var attr = new ProductScopeAttribute("  truckmate  ");

        Assert.Equal("truckmate", attr.ProductId);
    }

    /// <summary>FR-1 Null product identifier throws ArgumentNullException.</summary>
    [Fact]
    public void ConstructorRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ProductScopeAttribute(null!));
    }

    /// <summary>FR-1 Empty product identifier throws ArgumentException.</summary>
    [Fact]
    public void ConstructorRejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new ProductScopeAttribute(""));
    }

    /// <summary>FR-1 Whitespace-only product identifier throws ArgumentException.</summary>
    [Fact]
    public void ConstructorRejectsWhitespaceOnly()
    {
        Assert.Throws<ArgumentException>(() => new ProductScopeAttribute("   "));
    }

    /// <summary>FR-1 Attribute targets Assembly only.</summary>
    [Fact]
    public void AttributeTargetsAssemblyOnly()
    {
        var usage = (AttributeUsageAttribute)typeof(ProductScopeAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        Assert.Equal(AttributeTargets.Assembly, usage.ValidOn);
    }

    /// <summary>FR-1 Attribute does not allow multiple declarations on the same assembly.</summary>
    [Fact]
    public void AttributeDoesNotAllowMultiple()
    {
        var usage = (AttributeUsageAttribute)typeof(ProductScopeAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        Assert.False(usage.AllowMultiple);
    }
}
