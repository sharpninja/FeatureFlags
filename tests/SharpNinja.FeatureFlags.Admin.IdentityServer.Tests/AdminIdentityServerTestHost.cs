using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.IdentityServer;

namespace SharpNinja.FeatureFlags.Admin.IdentityServer.Tests;

/// <summary>TR-9 TR-11: Self-contained in-process Admin IdentityServer host backed by SQLite-in-memory for test isolation.</summary>
internal sealed class AdminIdentityServerTestHost : IAsyncDisposable
{
    /// <summary>TR-9: stable seed user identifier for the embedded admin tenant.</summary>
    public const string SeedUserId = "admin-seed-user";

    /// <summary>TR-9: seed user password used by the auth code login flow.</summary>
    public const string SeedPassword = "SharpNinja!2026";

    /// <summary>TR-9: client_credentials secret for service-to-service tests.</summary>
    public const string ServiceClientSecret = "service-client-secret-test";

    private readonly IHost host;
    private readonly DbConnection sqliteConnection;

    private AdminIdentityServerTestHost(IHost host, DbConnection sqliteConnection)
    {
        this.host = host;
        this.sqliteConnection = sqliteConnection;
    }

    /// <summary>Underlying test server.</summary>
    public TestServer TestServer => host.GetTestServer();

    /// <summary>HTTP client targeting the test host root.</summary>
    public HttpClient CreateClient() => TestServer.CreateClient();

    /// <summary>Issuer URI baked into discovery + token responses.</summary>
    public string Issuer { get; private set; } = "";

    /// <summary>TR-9: builds and starts the test host.</summary>
    public static async Task<AdminIdentityServerTestHost> StartAsync()
    {
        DbConnection connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync().ConfigureAwait(false);

        IHostBuilder hostBuilder = Host.CreateDefaultBuilder();
        hostBuilder.ConfigureWebHost(webHost =>
        {
            webHost.UseTestServer();
            webHost.ConfigureServices((context, services) =>
            {
                services.AddRouting();

                services.AddSharpNinjaAdminIdentityServer(options =>
                {
                    SeedData.ApplyDefaults(
                        options,
                        adminClientRedirectUris: ["http://admin-blazor.test/signin-oidc"],
                        adminClientPostLogoutRedirectUris: ["http://admin-blazor.test/signout-callback-oidc"],
                        serviceClientSecret: ServiceClientSecret);
                });

                services.AddDbContext<AdminIdentityDbContext>(dbOptions =>
                {
                    dbOptions.UseSqlite(connection);
                });

                services.AddSharpNinjaFeatureFlagsAdminRuntime(
                    runtimeOptions =>
                    {
                        runtimeOptions.Authentication.Mode = AdminAuthenticationMode.Oidc;
                        runtimeOptions.Authentication.AuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
                        runtimeOptions.Authentication.Oidc.Authority = "http://localhost";
                        runtimeOptions.Authentication.Oidc.ClientId = SeedData.AdminClientId;
                    },
                    configureAuthentication: authBuilder =>
                    {
                        authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
                        {
                            jwt.Authority = "http://localhost";
                            jwt.RequireHttpsMetadata = false;
                            jwt.MapInboundClaims = false;
                            jwt.TokenValidationParameters.ValidIssuer = "http://localhost";
                            jwt.TokenValidationParameters.ValidateAudience = false;
                            jwt.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
                            jwt.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
                        });
                    });
            });

            webHost.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseSharpNinjaAdminIdentityServer();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/admin/ping", (HttpContext ctx) => Results.Text(
                        ctx.User.Identity?.Name ?? "anonymous"))
                        .RequireAuthorization(AdminPolicyNames.Read);
                });
            });
        });

        IHost host = await hostBuilder.StartAsync().ConfigureAwait(false);
        var instance = new AdminIdentityServerTestHost(host, connection)
        {
            Issuer = "http://localhost",
        };

        await AdminIdentityServerApplicationBuilderExtensions
            .EnsureAdminIdentityDatabaseAsync(
                host.Services,
                new SharpNinjaAdminUser
                {
                    Id = SeedUserId,
                    UserName = "admin@sharpninja.test",
                    NormalizedUserName = "ADMIN@SHARPNINJA.TEST",
                    Email = "admin@sharpninja.test",
                    NormalizedEmail = "ADMIN@SHARPNINJA.TEST",
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    DisplayName = "Admin Seed",
                    TenantId = "tenant-test",
                    Products = "truckmate,drivermate",
                    Roles = "editor,publisher,key-admin",
                },
                SeedPassword)
            .ConfigureAwait(false);

        return instance;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await host.StopAsync().ConfigureAwait(false);
        host.Dispose();
        await sqliteConnection.DisposeAsync().ConfigureAwait(false);
    }
}
