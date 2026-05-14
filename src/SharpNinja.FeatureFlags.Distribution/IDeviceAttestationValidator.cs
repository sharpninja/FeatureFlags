namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 TR-11 v1 provider-ready contract for Play Integrity, App Attest, or test attestation validators.</summary>
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
