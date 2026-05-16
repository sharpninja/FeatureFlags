using System.Collections.ObjectModel;

namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-1 v1 contract: canonical product identifiers supported by the first release scope.</summary>
/// <remarks>
/// Stateless after construction; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-1"/>
/// </remarks>
public static class SharpNinjaProductCatalog
{
    private static readonly ReadOnlyCollection<string> ProductIds =
        Array.AsReadOnly([TruckMate, DriverMate]);

    /// <summary>FR-1 v1 contract: TruckMate product identifier.</summary>
    public const string TruckMate = "truckmate";

    /// <summary>FR-1 v1 contract: DriverMate product identifier.</summary>
    public const string DriverMate = "drivermate";

    /// <summary>FR-1 v1 contract: all product identifiers in v1 scope.</summary>
    public static IReadOnlyCollection<string> V1ProductIds => ProductIds;

    /// <summary>FR-1 v1 contract: determines whether a product identifier is in the v1 catalog.</summary>
    /// <param name="productId">Product identifier to check.</param>
    /// <returns><see langword="true" /> when the product identifier is in the v1 catalog.</returns>
    public static bool IsV1Product(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        foreach (string candidate in ProductIds)
        {
            if (string.Equals(candidate, productId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
