namespace SharpNinja.FeatureFlags.Distribution;

/// <summary>TR-9 v1 policy decision that says whether a Distribution request must pass device attestation.</summary>
/// <param name="RequiresValidation">Whether a validator must accept the supplied attestation token.</param>
/// <param name="FailureCode">Optional failure code when policy rejects before validator execution.</param>
public sealed record DeviceAttestationPolicyDecision(
    bool RequiresValidation,
    string? FailureCode = null);
