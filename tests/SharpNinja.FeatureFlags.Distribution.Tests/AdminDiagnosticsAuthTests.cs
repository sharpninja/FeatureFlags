using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SharpNinja.FeatureFlags.Distribution.Tests;

/// <summary>TR-10 TR-11: Verifies that the Distribution /admin/diagnostics endpoint requires authentication.</summary>
public sealed class AdminDiagnosticsAuthTests : IClassFixture<DistributionWebFactory>
{
    private readonly DistributionWebFactory factory;

    /// <summary>Initializes a new <see cref="AdminDiagnosticsAuthTests"/>.</summary>
    /// <param name="factory">Web application factory shared across all tests in this class.</param>
    public AdminDiagnosticsAuthTests(DistributionWebFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.factory = factory;
    }

    /// <summary>An anonymous request to /admin/diagnostics is rejected with 401.</summary>
    [Fact]
    public async Task AnonymousRequestReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/admin/diagnostics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A request bearing a syntactically invalid token is rejected with 401.</summary>
    [Fact]
    public async Task InvalidBearerTokenReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/diagnostics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");
        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The anonymous /, /health, /metrics endpoints remain reachable without a token.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/metrics")]
    public async Task AnonymousRoutesRemainPublic(string path)
    {
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>TR-10 TR-11: WebApplicationFactory test fixture for the Distribution host.</summary>
public sealed class DistributionWebFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseTestServer();
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminIdentityServer:Authority"] = "http://localhost",
                ["AdminIdentityServer:PublicIssuer"] = "http://localhost",
                ["AdminIdentityServer:Audience"] = "sharpninja.admin.api",
            });
        });
    }
}
