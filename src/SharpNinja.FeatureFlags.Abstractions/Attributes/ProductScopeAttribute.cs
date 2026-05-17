namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>
/// FR-1 FR-10 TR-1: stamps a CQRS Command / Query / Handler, an aiUnit test
/// method / class, or a consuming assembly with the set of products for which
/// it is active. Dispatchers, build tools (<c>flagctl</c>, Roslyn generators),
/// and aiUnit pickers consult the attribute via reflection (assembly to class
/// to method, with method-level scope winning).
/// </summary>
/// <remarks>
/// <para>
/// v1.0.3 widens the v1.0.2 contract:
/// <list type="bullet">
///   <item><description><see cref="AttributeTargets"/> now permits <c>Class</c>, <c>Method</c>, and <c>Assembly</c> so CQRS types and aiUnit test methods can tag themselves directly.</description></item>
///   <item><description>The constructor accepts <c>params string[] products</c> so a single attribute can carry multiple product ids (multi-product-shared use case).</description></item>
///   <item><description>A <see cref="MatchesProduct(string?)"/> helper short-circuits the common scope-check call site.</description></item>
///   <item><description>The type lives in the canonical <c>SharpNinja.FeatureFlags.Abstractions</c> namespace (was <c>.Abstractions.Attributes</c> in v1.0.2).</description></item>
/// </list>
/// </para>
/// <para>
/// Usage:
/// <code>
/// [ProductScope("truckmate")]
/// public sealed record StartTripCommand(string DriverId) : ICommand&lt;TripStartedResult&gt;;
///
/// [ProductScope("truckmate", "drivermate")]
/// public sealed record AddTodoCommand(string Title) : ICommand&lt;TodoAddedResult&gt;;
///
/// [assembly: ProductScope("truckmate")]
/// </code>
/// </para>
/// <para>
/// Each product id should match an entry in <see cref="SharpNinja.FeatureFlags.Abstractions.Options.SharpNinjaProductCatalog"/>.
/// Empty <see cref="Products"/> is treated as no restriction (equivalent to omitting the attribute).
/// </para>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-1"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-1"/>
/// </remarks>
[AttributeUsage(
	AttributeTargets.Class
		| AttributeTargets.Method
		| AttributeTargets.Assembly,
	AllowMultiple = false,
	Inherited = false)]
public sealed class ProductScopeAttribute : Attribute
{
	/// <summary>
	/// Initialises the attribute with the supplied product identifiers.
	/// Each id must be a non-empty, case-insensitive product catalog entry.
	/// </summary>
	/// <param name="products">One or more product identifiers (e.g. <c>"truckmate"</c>, <c>"drivermate"</c>).</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="products"/> is null.</exception>
	public ProductScopeAttribute(params string[] products)
	{
		ArgumentNullException.ThrowIfNull(products);
		Products = ValidateProducts(products);
	}

	/// <summary>
	/// The set of product identifiers this scope covers. Read-only.
	/// </summary>
	public IReadOnlyList<string> Products { get; }

	/// <summary>
	/// Convenience accessor returning the first product id. Throws when
	/// <see cref="Products"/> is empty. Useful for the single-product
	/// assembly-level call site that v1.0.2 was scoped to.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when no products are declared.</exception>
	public string ProductId
	{
		get
		{
			if (Products.Count == 0)
			{
				throw new InvalidOperationException("ProductScopeAttribute declared with no products; ProductId is undefined.");
			}
			return Products[0];
		}
	}

	/// <summary>
	/// True when the supplied <paramref name="productId"/> is contained in
	/// <see cref="Products"/> (ordinal comparison). Empty <see cref="Products"/>
	/// returns true (no restriction); null or empty <paramref name="productId"/>
	/// against a non-empty <see cref="Products"/> returns false.
	/// </summary>
	/// <param name="productId">The product to check (e.g. resolved ProductId from <c>SharpNinjaFeatureFlagOptions</c>).</param>
	public bool MatchesProduct(string? productId)
	{
		if (Products.Count == 0)
		{
			return true;
		}
		if (string.IsNullOrEmpty(productId))
		{
			return false;
		}
		foreach (string scopedId in Products)
		{
			if (string.Equals(scopedId, productId, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static string[] ValidateProducts(string[] products)
	{
		for (int i = 0; i < products.Length; i++)
		{
			string raw = products[i];
			ArgumentException.ThrowIfNullOrWhiteSpace(raw, $"products[{i}]");
			products[i] = raw.Trim();
		}
		return products;
	}
}
