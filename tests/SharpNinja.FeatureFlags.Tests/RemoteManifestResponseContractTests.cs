using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Abstractions.Options;
using Xunit;

namespace SharpNinja.FeatureFlags.Tests;

/// <summary>TEST-GATEWAY-006: Distribution and Android SDK agree on the signed manifest wire response.</summary>
public sealed class RemoteManifestResponseContractTests
{
    /// <summary>Raw signed manifest JSON is parsed into the SDK envelope without an API key.</summary>
    [Fact]
    public async Task RawSignedProductionManifestFromDistributionIsParsedAsAnEnvelope()
    {
        const string manifestJson =
            """
            {"schemaVersion":1,"productId":"gigdriving","releaseId":"gigdriving-1.0.0-stable-0","environment":"Production","flags":[],"signature":{"algorithm":"Ed25519","keyId":"test-key","value":"AQIDBA=="}}
            """;
        var options = new SharpNinjaFeatureFlagOptions(
            "gigdriving",
            "gigdriving-1.0.0-stable-0",
            "Production",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30))
        {
            DistributionBaseUri = new Uri("http://localhost:7148/"),
            SupportedProductIds = ["gigdriving"],
        };
        using var client = new HttpClient(new SignedManifestHandler(manifestJson));
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton(client)
            .AddSharpNinjaFeatureFlags(options, manifestJson)
            .BuildServiceProvider();

        ISharpNinjaRemoteManifestClient remote = provider.GetRequiredService<ISharpNinjaRemoteManifestClient>();
        RemoteManifestFetchResult result = await remote.FetchAsync(
            options,
            forceRefresh: true,
            currentETag: null);

        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Envelope);
        Assert.Equal(manifestJson, result.Envelope.ManifestJson);
        Assert.Equal("AQIDBA==", result.Envelope.Signature);
        Assert.Equal("test-key", result.Envelope.SigningKeyId);
        Assert.Equal("Ed25519", result.Envelope.Algorithm);
    }

    private sealed class SignedManifestHandler(string manifestJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(
                "http://localhost:7148/v1/manifest/gigdriving/gigdriving-1.0.0-stable-0?environment=Production",
                request.RequestUri?.ToString());
            Assert.False(request.Headers.Contains("X-Api-Key"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(manifestJson),
            });
        }
    }
}
