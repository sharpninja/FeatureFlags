namespace SharpNinja.FeatureFlags.Abstractions.Attributes;

/// <summary>
/// FR-1 FR-10 TR-1: stamps a consuming assembly with its SharpNinja product identity.
/// Apply once at the assembly level so build tools, <c>flagctl</c>, and Roslyn generators
/// can read the product ID without requiring an MSBuild property.
/// </summary>
/// <remarks>
/// Usage: <c>[assembly: ProductScope("truckmate")]</c>
/// The product ID must match an entry in <see cref="SharpNinja.FeatureFlags.Abstractions.Options.SharpNinjaProductCatalog"/>.
/// </remarks>
/// <param name="productId">
/// The product identifier for this assembly. Must be a non-empty, case-insensitive product catalog entry.
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class ProductScopeAttribute(string productId) : Attribute
{
    /// <summary>Gets the product identifier declared for this assembly.</summary>
    public string ProductId { get; } = ValidateProductId(productId);

    private static string ValidateProductId(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return productId.Trim();
    }
}
