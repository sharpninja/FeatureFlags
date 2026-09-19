using System.Text.Json;
using System.Text.Json.Nodes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Xunit;

namespace SharpNinja.FeatureFlags.Manifest.Tests;

/// <summary>TEST-GATEWAY-006: signed manifests verify across Windows and Linux canonical newlines.</summary>
public sealed class SignedManifestEd25519VerifierTests
{
    /// <summary>Both new LF signatures and already-issued Windows CRLF signatures remain valid.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void VerifyAcceptsCanonicalSignaturesFromBothPlatforms(string canonicalNewLine)
    {
        (string manifestJson, byte[] publicKey) = CreateSignedManifest(canonicalNewLine);

        Assert.True(SignedManifestEd25519Verifier.Verify(manifestJson, publicKey));
    }

    /// <summary>Neither newline compatibility path may accept changed signed content.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void VerifyRejectsTamperedContentFromBothPlatforms(string canonicalNewLine)
    {
        (string manifestJson, byte[] publicKey) = CreateSignedManifest(canonicalNewLine);
        JsonObject tampered = (JsonObject)JsonNode.Parse(manifestJson)!;
        tampered["releaseId"] = "gigdriving-altered";

        Assert.False(SignedManifestEd25519Verifier.Verify(tampered.ToJsonString(), publicKey));
    }

    private static (string ManifestJson, byte[] PublicKey) CreateSignedManifest(string canonicalNewLine)
    {
        byte[] seed = Enumerable.Range(1, 32).Select(static value => (byte)value).ToArray();
        var privateKey = new Ed25519PrivateKeyParameters(seed, 0);
        var signature = new JsonObject
        {
            ["algorithm"] = "Ed25519",
            ["keyId"] = "test-key",
            ["value"] = string.Empty,
        };
        var manifest = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["productId"] = "gigdriving",
            ["releaseId"] = "gigdriving-1.0.0-stable-0",
            ["environment"] = "Production",
            ["flags"] = new JsonArray(),
            ["signature"] = signature,
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            NewLine = canonicalNewLine,
            SkipValidation = false,
        }))
        {
            manifest.WriteTo(writer);
        }

        byte[] canonicalBytes = stream.ToArray();
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(canonicalBytes, 0, canonicalBytes.Length);
        signature["value"] = Convert.ToBase64String(signer.GenerateSignature());

        return (manifest.ToJsonString(), privateKey.GeneratePublicKey().GetEncoded());
    }
}
