using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Data;
using SharpNinja.FeatureFlags.Admin.Data.Postgres;
using SharpNinja.FeatureFlags.Admin.IdentityServer;
using SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddNgrokTunneling();

string issuer = builder.Configuration["AdminIdentityServer:Authority"] ?? "http://admin:8080";
string publicIssuer = builder.Configuration["AdminIdentityServer:PublicIssuer"] ?? issuer;
string adminAudience = builder.Configuration["AdminIdentityServer:Audience"] ?? SeedData.AdminApiScope;
string? duendeLicenseKey = builder.Configuration["Duende:LicenseKey"];
string serviceClientSecret = builder.Configuration["AdminIdentityServer:ServiceClientSecret"]
    ?? throw new InvalidOperationException("Admin service client secret is required.");

string postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Admin PostgreSQL connection string is required.");
if (string.IsNullOrWhiteSpace(postgres))
{
    throw new InvalidOperationException("Admin PostgreSQL connection string is required.");
}

string signingCertificatePath = builder.Configuration["AdminIdentityServer:SigningCertificate:Path"]
    ?? throw new InvalidOperationException("Admin signing certificate path is required.");
string signingCertificatePassword = builder.Configuration["AdminIdentityServer:SigningCertificate:Password"]
    ?? throw new InvalidOperationException("Admin signing certificate password is required.");
if (!File.Exists(signingCertificatePath) || string.IsNullOrWhiteSpace(signingCertificatePassword))
{
    throw new InvalidOperationException("Admin signing certificate must exist and have a protected password.");
}

builder.Services.AddDbContext<PostgresAdminDbContext>(options =>
    options.UseNpgsql(postgres, provider =>
        provider.MigrationsAssembly(typeof(PostgresAdminDbContext).Assembly.FullName)));
builder.Services.AddScoped<AdminDbContext>(services =>
    services.GetRequiredService<PostgresAdminDbContext>());
builder.Services.AddDbContext<PostgresAdminIdentityDbContext>(options =>
    options.UseNpgsql(postgres, provider =>
        provider.MigrationsAssembly(typeof(PostgresAdminIdentityDbContext).Assembly.FullName)));
builder.Services.AddScoped<AdminIdentityDbContext>(services =>
    services.GetRequiredService<PostgresAdminIdentityDbContext>());

builder.Services.AddSharpNinjaAdminIdentityServer(options =>
{
    options.LicenseKey = duendeLicenseKey;
    options.SigningCertificate.Path = signingCertificatePath;
    options.SigningCertificate.Password = signingCertificatePassword;
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
builder.Services.RemoveAll<IAdminRuntimeStore>();
builder.Services.RemoveAll<IAdminRuntimeService>();
builder.Services.AddScoped<IAdminRuntimeStore, EfCoreAdminRuntimeStore>();
builder.Services.AddScoped<IAdminRuntimeService, InMemoryAdminRuntimeService>();

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    PostgresAdminDbContext adminDb = scope.ServiceProvider.GetRequiredService<PostgresAdminDbContext>();
    if (!adminDb.Database.GetMigrations().Any())
    {
        throw new InvalidOperationException("Admin PostgreSQL migrations were not discovered.");
    }

    await adminDb.Database.MigrateAsync().ConfigureAwait(false);

    PostgresAdminIdentityDbContext identityDb =
        scope.ServiceProvider.GetRequiredService<PostgresAdminIdentityDbContext>();
    if (!identityDb.Database.GetMigrations().Any())
    {
        throw new InvalidOperationException("Admin Identity PostgreSQL migrations were not discovered.");
    }
}

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
        .Select(static child => child.Value)
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value!)
        .ToArray();

    return values.Length == 0 ? null : values;
}
