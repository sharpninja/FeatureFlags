namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 v1 result returned by a device-attestation validator.</summary>
/// <param name="Succeeded">Whether the supplied attestation token was accepted.</param>
/// <param name="FailureCode">Optional stable failure code.</param>
public sealed record DeviceAttestationValidationResult(
    bool Succeeded,
    string? FailureCode = null);
