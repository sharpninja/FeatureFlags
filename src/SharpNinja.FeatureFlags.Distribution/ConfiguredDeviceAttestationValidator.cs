using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class ConfiguredDeviceAttestationValidator : IDeviceAttestationValidator
{
    private readonly IOptions<SharpNinjaDistributionOptions> options;

    public ConfiguredDeviceAttestationValidator(IOptions<SharpNinjaDistributionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    public ValueTask<DeviceAttestationValidationResult> ValidateAsync(
        DeviceAttestationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return ValueTask.FromResult(new DeviceAttestationValidationResult(false, "missing_device_attestation"));
        }

        if (!TryReadAllowedTokens(context.ProductId, out IReadOnlyList<string>? configuredTokens))
        {
            return ValueTask.FromResult(new DeviceAttestationValidationResult(false, "device_attestation_not_configured"));
        }

        foreach (string configuredToken in configuredTokens)
        {
            if (FixedTimeEquals(configuredToken, context.Token))
            {
                return ValueTask.FromResult(new DeviceAttestationValidationResult(true));
            }
        }

        return ValueTask.FromResult(new DeviceAttestationValidationResult(false, "invalid_device_attestation"));
    }

    private bool TryReadAllowedTokens(
        string productId,
        [NotNullWhen(true)] out IReadOnlyList<string>? configuredTokens)
    {
        if (options.Value.DeviceAttestationTestTokens.TryGetValue(productId, out configuredTokens)
            && configuredTokens.Count > 0)
        {
            return true;
        }

        return options.Value.DeviceAttestationTestTokens.TryGetValue("*", out configuredTokens)
            && configuredTokens.Count > 0;
    }

    private static bool FixedTimeEquals(string expectedToken, string suppliedToken)
    {
        if (expectedToken.Length != suppliedToken.Length)
        {
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        byte[] suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
