using Microsoft.Extensions.Options;

namespace SharpNinja.FeatureFlags.Distribution;

internal sealed class OptionsDeviceAttestationPolicy : IDeviceAttestationPolicy
{
    private readonly IOptions<SharpNinjaDistributionOptions> options;

    public OptionsDeviceAttestationPolicy(IOptions<SharpNinjaDistributionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    public ValueTask<DeviceAttestationPolicyDecision> EvaluateAsync(
        DeviceAttestationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Value.RequireDeviceAttestation)
        {
            return ValueTask.FromResult(new DeviceAttestationPolicyDecision(RequiresValidation: false));
        }

        if (string.IsNullOrWhiteSpace(context.Token))
        {
            return ValueTask.FromResult(new DeviceAttestationPolicyDecision(
                RequiresValidation: true,
                FailureCode: "missing_device_attestation"));
        }

        return ValueTask.FromResult(new DeviceAttestationPolicyDecision(RequiresValidation: true));
    }
}
