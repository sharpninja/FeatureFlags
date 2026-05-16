using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Blazor.Components;
using SharpNinja.FeatureFlags.Admin.Blazor.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

string authority = builder.Configuration["AdminIdentityServer:Authority"] ?? "http://admin:8080";
string clientId = builder.Configuration["AdminIdentityServer:ClientId"] ?? "sharpninja-admin";

builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime(
    options =>
    {
        options.Authentication.Mode = AdminAuthenticationMode.Oidc;
        options.Authentication.AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authentication.ChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.Authentication.ForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Authentication.Oidc.Authority = authority;
        options.Authentication.Oidc.ClientId = clientId;
    },
    configureAuthentication: authBuilder =>
    {
        authBuilder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
        {
            oidc.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            oidc.Authority = authority;
            oidc.ClientId = clientId;
            oidc.ResponseType = "code";
            oidc.UsePkce = true;
            oidc.RequireHttpsMetadata = false;
            oidc.MapInboundClaims = false;
            oidc.SaveTokens = true;
            oidc.GetClaimsFromUserInfoEndpoint = true;
            oidc.Scope.Clear();
            oidc.Scope.Add("openid");
            oidc.Scope.Add("profile");
            oidc.Scope.Add("sharpninja_rbac");
            oidc.Scope.Add("sharpninja.admin.api");
            oidc.TokenValidationParameters.NameClaimType = "name";
            oidc.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
            oidc.CallbackPath = "/signin-oidc";
            oidc.SignedOutCallbackPath = "/signout-callback-oidc";
        });
    });

builder.Services.AddScoped<AdminRuntimeAccessor>();
builder.Services.AddCascadingAuthenticationState();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);
