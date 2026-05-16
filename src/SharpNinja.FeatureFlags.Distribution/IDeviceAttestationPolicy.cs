namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 provider-ready policy contract for deciding when device attestation is required.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
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
