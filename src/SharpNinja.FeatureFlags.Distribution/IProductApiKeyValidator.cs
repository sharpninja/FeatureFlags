namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 provider-ready contract for product-scoped Distribution API key validation.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface IProductApiKeyValidator
{
    /// <summary>TR-9 validates that an API key is authorized for the requested product.</summary>
    /// <param name="productId">Requested product identifier.</param>
    /// <param name="apiKey">Supplied API key or bearer token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the key is authorized for the product.</returns>
    ValueTask<bool> ValidateAsync(string productId, string apiKey, CancellationToken cancellationToken);
}
