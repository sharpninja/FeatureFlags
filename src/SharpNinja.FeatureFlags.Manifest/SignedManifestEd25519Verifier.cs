using System.Text.Json;
using System.Text.Json.Nodes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace SharpNinja.FeatureFlags.Manifest;

/// <summary>FR-GATEWAY-006, TR-GATEWAY-OPS-005: verifies LF and legacy CRLF Ed25519 signed manifests.</summary>
public static class SignedManifestEd25519Verifier
{
    /// <summary>Returns true only when the signed JSON matches the trusted raw public key.</summary>
    public static bool Verify(string manifestJson, ReadOnlySpan<byte> publicKeyBytes)
    {
        if (string.IsNullOrWhiteSpace(manifestJson) || publicKeyBytes.Length != 32)
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(manifestJson) is not JsonObject root
                || root["signature"] is not JsonObject signature)
            {
                return false;
            }

            string? algorithm = signature["algorithm"]?.GetValue<string>();
            string? keyId = signature["keyId"]?.GetValue<string>();
            string? encoded = signature["value"]?.GetValue<string>();
            if (!string.Equals(algorithm, "Ed25519", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(keyId)
                || string.IsNullOrWhiteSpace(encoded))
            {
                return false;
            }

            byte[] signatureBytes = Convert.FromBase64String(encoded);
            if (signatureBytes.Length != 64)
            {
                return false;
            }

            signature["value"] = string.Empty;
            return VerifyCanonical(root, publicKeyBytes, signatureBytes, "\n")
                || VerifyCanonical(root, publicKeyBytes, signatureBytes, "\r\n");
        }
        catch (Exception exception) when (exception is JsonException
            or FormatException
            or ArgumentException
            or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool VerifyCanonical(
        JsonObject root,
        ReadOnlySpan<byte> publicKeyBytes,
        byte[] signatureBytes,
        string newLine)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = newLine,
            SkipValidation = false,
        }))
        {
            root.WriteTo(writer);
        }

        byte[] canonical = stream.ToArray();
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, new Ed25519PublicKeyParameters(publicKeyBytes.ToArray(), 0));
        verifier.BlockUpdate(canonical, 0, canonical.Length);
        return verifier.VerifySignature(signatureBytes);
    }
}
