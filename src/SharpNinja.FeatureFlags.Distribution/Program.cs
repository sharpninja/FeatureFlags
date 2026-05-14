using SharpNinja.FeatureFlags.Distribution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharpNinjaFeatureFlagDistribution();

var app = builder.Build();

app.MapSharpNinjaFeatureFlagDistributionEndpoints();

app.Run();
