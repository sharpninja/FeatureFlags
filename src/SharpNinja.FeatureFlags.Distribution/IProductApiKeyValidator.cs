namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 provider-ready contract for product-scoped Distribution API key validation.</summary>
public interface IProductApiKeyValidator
{
    /// <summary>TR-9 validates that an API key is authorized for the requested product.</summary>
    /// <param name="productId">Requested product identifier.</param>
    /// <param name="apiKey">Supplied API key or bearer token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the key is authorized for the product.</returns>
    ValueTask<bool> ValidateAsync(string productId, string apiKey, CancellationToken cancellationToken);
}
