namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 v1 result returned by a device-attestation validator.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
/// <param name="Succeeded">Whether the supplied attestation token was accepted.</param>
/// <param name="FailureCode">Optional stable failure code.</param>
public sealed record DeviceAttestationValidationResult(
    bool Succeeded,
    string? FailureCode = null);
