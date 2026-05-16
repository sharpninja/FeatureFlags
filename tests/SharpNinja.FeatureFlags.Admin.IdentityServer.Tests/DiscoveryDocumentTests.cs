using System.Text.Json;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer.Tests;

/// <summary>TR-9 TR-11: Verifies the embedded SharpNinja Admin IdentityServer publishes a valid OpenID Connect discovery document.</summary>
public sealed class DiscoveryDocumentTests
{
    /// <summary>The discovery endpoint returns 200 and includes the configured issuer.</summary>
    [Fact]
    public async Task DiscoveryEndpointReturnsConfiguredIssuer()
    {
        await using AdminIdentityServerTestHost host = await AdminIdentityServerTestHost.StartAsync();
        using HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/.well-known/openid-configuration", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);
        string issuer = document.RootElement.GetProperty("issuer").GetString() ?? "";

        Assert.Equal(host.Issuer, issuer);
    }

    /// <summary>The discovery document advertises the SharpNinja Admin API scope and RBAC identity resource.</summary>
    [Fact]
    public async Task DiscoveryDocumentAdvertisesSharpNinjaScopes()
    {
        await using AdminIdentityServerTestHost host = await AdminIdentityServerTestHost.StartAsync();
        using HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/.well-known/openid-configuration", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        var scopes = document.RootElement
            .GetProperty("scopes_supported")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(SeedData.AdminApiScope, scopes);
        Assert.Contains(SeedData.RbacIdentityResource, scopes);
        Assert.Contains("openid", scopes);
        Assert.Contains("profile", scopes);
    }
}
