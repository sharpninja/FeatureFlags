using SharpNinja.FeatureFlags.Admin;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNgrokTunneling();
builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime();

var app = builder.Build();

app.UseNgrokTunneling();
app.UseSharpNinjaFeatureFlagsAdminRuntime();

app.Run();
