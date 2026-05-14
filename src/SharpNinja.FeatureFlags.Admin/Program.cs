using SharpNinja.FeatureFlags.Admin;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime();

var app = builder.Build();

app.UseSharpNinjaFeatureFlagsAdminRuntime();

app.Run();
