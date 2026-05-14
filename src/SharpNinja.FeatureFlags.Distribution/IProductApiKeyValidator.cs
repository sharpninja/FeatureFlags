namespace SharpNinja.FeatureFlags.Distribution;

internal interface IProductApiKeyValidator
{
    ValueTask<bool> ValidateAsync(string productId, string apiKey, CancellationToken cancellationToken);
}
