using SharpNinja.FeatureFlags.Distribution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNgrokTunneling();
builder.Services.AddSharpNinjaFeatureFlagDistribution(builder.Configuration.GetSection("Distribution"));

var app = builder.Build();

app.UseNgrokTunneling();
app.MapSharpNinjaFeatureFlagDistributionEndpoints();

app.Run();
