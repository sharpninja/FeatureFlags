using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class OptionsProductApiKeyValidator : IProductApiKeyValidator
{
    private static readonly Action<ILogger, string, Exception?> ProductKeysMissing =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(ProductKeysMissing)),
            "Rejected Distribution request for {ProductId}: no product API keys are configured.");

    private static readonly Action<ILogger, string, Exception?> ProductKeyMismatch =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, nameof(ProductKeyMismatch)),
            "Rejected Distribution request for {ProductId}: product API key did not match.");

    private readonly IOptions<SharpNinjaDistributionOptions> options;
    private readonly ILogger<OptionsProductApiKeyValidator> logger;

    public OptionsProductApiKeyValidator(
        IOptions<SharpNinjaDistributionOptions> options,
        ILogger<OptionsProductApiKeyValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.options = options;
        this.logger = logger;
    }

    public ValueTask<bool> ValidateAsync(string productId, string apiKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(apiKey))
        {
            return ValueTask.FromResult(false);
        }

        if (!options.Value.ProductApiKeys.TryGetValue(productId, out IReadOnlyList<string>? productKeys)
            || productKeys.Count == 0)
        {
            ProductKeysMissing(logger, productId, null);
            return ValueTask.FromResult(false);
        }

        foreach (string expectedKey in productKeys)
        {
            if (FixedTimeEquals(expectedKey, apiKey))
            {
                return ValueTask.FromResult(true);
            }
        }

        ProductKeyMismatch(logger, productId, null);
        return ValueTask.FromResult(false);
    }

    private static bool FixedTimeEquals(string expectedKey, string suppliedKey)
    {
        if (expectedKey.Length != suppliedKey.Length)
        {
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
