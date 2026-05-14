namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-2 FR-3 TR-4 TR-11 v1 verifier for signed manifest envelopes.</summary>
public interface ISharpNinjaManifestSignatureVerifier
{
    /// <summary>TR-4 verifies whether an envelope can be trusted by the SDK.</summary>
    /// <param name="envelope">Signed manifest envelope.</param>
    /// <param name="errorMessage">Verification failure detail when verification fails.</param>
    /// <returns><see langword="true" /> when the envelope is trusted.</returns>
    bool Verify(SignedManifestEnvelope envelope, out string? errorMessage);
}
