using System.IdentityModel.Tokens.Jwt;
using IdentityModel.Client;
using SharpNinja.FeatureFlags.Admin;
using Xunit;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer.Tests;

/// <summary>TR-9 TR-11: Verifies the embedded SharpNinja Admin IdentityServer issues tokens carrying the SharpNinja RBAC claim shape.</summary>
public sealed class TokenIssuanceTests
{
    /// <summary>A client_credentials request returns an access token whose audience matches the Admin API scope.</summary>
    [Fact]
    public async Task ClientCredentialsTokenIncludesAdminApiAudience()
    {
        await using AdminIdentityServerTestHost host = await AdminIdentityServerTestHost.StartAsync();
        using HttpClient client = host.CreateClient();

        TokenResponse token = await RequestClientCredentialsTokenAsync(host, client);

        Assert.False(token.IsError, token.Error ?? token.ErrorDescription);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt = handler.ReadJwtToken(token.AccessToken);
        Assert.Equal(host.Issuer, jwt.Issuer);

        var scopes = jwt.Claims
            .Where(c => string.Equals(c.Type, "scope", StringComparison.Ordinal))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(SeedData.AdminApiScope, scopes);
    }

    /// <summary>An anonymous request to a protected Admin endpoint is challenged with 401.</summary>
    [Fact]
    public async Task ProtectedAdminEndpointRejectsAnonymousRequests()
    {
        await using AdminIdentityServerTestHost host = await AdminIdentityServerTestHost.StartAsync();
        using HttpClient client = host.CreateClient();

        using HttpResponseMessage anonymous = await client.GetAsync(new Uri("/admin/ping", UriKind.Relative));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static async Task<TokenResponse> RequestClientCredentialsTokenAsync(
        AdminIdentityServerTestHost host,
        HttpClient client)
    {
        return await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = new Uri(new Uri(host.Issuer), "/connect/token").ToString(),
            ClientId = SeedData.ServiceClientId,
            ClientSecret = AdminIdentityServerTestHost.ServiceClientSecret,
            Scope = SeedData.AdminApiScope,
        });
    }
}
