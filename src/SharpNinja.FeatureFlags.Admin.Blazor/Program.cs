using SharpNinja.FeatureFlags.Admin;
using SharpNinja.FeatureFlags.Admin.Blazor.Components;
using SharpNinja.FeatureFlags.Admin.Blazor.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime();
builder.Services.AddScoped<AdminRuntimeAccessor>();

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync().ConfigureAwait(false);
