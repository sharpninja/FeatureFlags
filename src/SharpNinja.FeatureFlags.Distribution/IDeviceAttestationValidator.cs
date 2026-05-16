namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 provider-ready contract for Play Integrity, App Attest, or test attestation validators.</summary>
/// <remarks>
/// v1 contract. Implementations are responsible for documenting their own thread-safety and lifecycle.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public interface IDeviceAttestationValidator
{
    /// <summary>TR-9 validates an opaque device-attestation token for one Distribution request.</summary>
    /// <param name="context">Device-attestation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    ValueTask<DeviceAttestationValidationResult> ValidateAsync(
        DeviceAttestationContext context,
        CancellationToken cancellationToken);
}
