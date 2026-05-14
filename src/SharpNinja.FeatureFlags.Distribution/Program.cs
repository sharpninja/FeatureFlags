using SharpNinja.FeatureFlags.Distribution;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharpNinjaFeatureFlagDistribution(builder.Configuration.GetSection("Distribution"));

var app = builder.Build();

app.MapSharpNinjaFeatureFlagDistributionEndpoints();

app.Run();
