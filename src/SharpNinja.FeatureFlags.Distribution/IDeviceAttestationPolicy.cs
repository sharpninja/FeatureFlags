namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 provider-ready policy contract for deciding when device attestation is required.</summary>
public interface IDeviceAttestationPolicy
{
    /// <summary>TR-9 evaluates the attestation requirement for one Distribution request.</summary>
    /// <param name="context">Device-attestation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attestation policy decision.</returns>
    ValueTask<DeviceAttestationPolicyDecision> EvaluateAsync(
        DeviceAttestationContext context,
        CancellationToken cancellationToken);
}
