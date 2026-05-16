using Microsoft.AspNetCore.Authentication.JwtBearer;
using SharpNinja.FeatureFlags.Distribution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNgrokTunneling();
builder.Services.AddSharpNinjaFeatureFlagDistribution(builder.Configuration.GetSection("Distribution"));

string adminIssuer = builder.Configuration["AdminIdentityServer:Authority"] ?? "http://admin:8080";
string publicIssuer = builder.Configuration["AdminIdentityServer:PublicIssuer"] ?? adminIssuer;
string adminAudience = builder.Configuration["AdminIdentityServer:Audience"] ?? "sharpninja.admin.api";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
    {
        jwtOptions.Authority = adminIssuer;
        jwtOptions.RequireHttpsMetadata = false;
        jwtOptions.MapInboundClaims = false;
        jwtOptions.TokenValidationParameters.ValidIssuers = new[] { adminIssuer, publicIssuer };
        jwtOptions.Audience = adminAudience;
        jwtOptions.TokenValidationParameters.ValidateAudience = false;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseNgrokTunneling();
app.UseAuthentication();
app.UseAuthorization();
app.MapSharpNinjaFeatureFlagDistributionEndpoints();
app.MapSharpNinjaAdminDiagnostics();

app.Run();

/// <summary>TR-9 TR-10 TR-11: Test host entry point.</summary>
/// <remarks>
/// Stateless after construction; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-10"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
public partial class Program;
