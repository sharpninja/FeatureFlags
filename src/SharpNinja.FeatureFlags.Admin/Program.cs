using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.IdentityServer;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddNgrokTunneling();

string issuer = builder.Configuration["AdminIdentityServer:Authority"] ?? "http://admin:8080";
string publicIssuer = builder.Configuration["AdminIdentityServer:PublicIssuer"] ?? issuer;
string adminAudience = builder.Configuration["AdminIdentityServer:Audience"] ?? SeedData.AdminApiScope;
string? duendeLicenseKey = builder.Configuration["Duende:LicenseKey"];
string serviceClientSecret = builder.Configuration["AdminIdentityServer:ServiceClientSecret"]
    ?? "sharpninja-admin-service-secret-change-me";

builder.Services.AddDbContext<AdminIdentityDbContext>(dbOptions =>
{
    dbOptions.UseInMemoryDatabase("sharpninja-admin-identity");
});

builder.Services.AddSharpNinjaAdminIdentityServer(options =>
{
    options.LicenseKey = duendeLicenseKey;
    string[] redirectUris = ReadStringArray(builder.Configuration, "AdminIdentityServer:RedirectUris")
        ?? ["http://admin-blazor:8080/signin-oidc"];
    string[] postLogoutRedirectUris = ReadStringArray(builder.Configuration, "AdminIdentityServer:PostLogoutRedirectUris")
        ?? ["http://admin-blazor:8080/signout-callback-oidc"];

    SeedData.ApplyDefaults(
        options,
        adminClientRedirectUris: redirectUris,
        adminClientPostLogoutRedirectUris: postLogoutRedirectUris,
        serviceClientSecret: serviceClientSecret);
});

builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime(
    options =>
    {
        options.Authentication.Mode = AdminAuthenticationMode.Oidc;
        options.Authentication.AuthenticationScheme = JwtBearerDefaults.AuthenticationScheme;
        options.Authentication.Oidc.Authority = issuer;
        options.Authentication.Oidc.ClientId = SeedData.AdminClientId;
    },
    configureAuthentication: authBuilder =>
    {
        authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
        {
            jwt.Authority = issuer;
            jwt.RequireHttpsMetadata = false;
            jwt.MapInboundClaims = false;
            jwt.Audience = adminAudience;
            jwt.TokenValidationParameters.ValidIssuers = new[] { issuer, publicIssuer };
            jwt.TokenValidationParameters.ValidateAudience = false;
        });
    });

WebApplication app = builder.Build();

await AdminIdentityServerApplicationBuilderExtensions
    .EnsureAdminIdentityDatabaseAsync(app.Services, seedUser: null, seedPassword: null)
    .ConfigureAwait(false);

app.UseNgrokTunneling();
app.UseRouting();
app.UseAuthentication();
app.UseSharpNinjaAdminIdentityServer();
app.UseAuthorization();
app.UseSharpNinjaFeatureFlagsAdminRuntime();

app.Run();

static string[]? ReadStringArray(IConfiguration configuration, string key)
{
    IConfigurationSection section = configuration.GetSection(key);
    string[] values = section
        .GetChildren()
        .Select(static c => c.Value)
        .Where(static v => !string.IsNullOrWhiteSpace(v))
        .Select(static v => v!)
        .ToArray();

    return values.Length == 0 ? null : values;
}
