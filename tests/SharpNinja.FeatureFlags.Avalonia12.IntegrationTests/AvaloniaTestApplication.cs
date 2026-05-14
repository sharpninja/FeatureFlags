using Avalonia;
using Avalonia.Headless;
using SharpNinja.FeatureFlags.Samples.Avalonia12;

namespace SharpNinja.FeatureFlags.Avalonia12.IntegrationTests;

/// <summary>Configures Avalonia Headless for xUnit-rendered sample window tests.</summary>
public static class AvaloniaTestApplication
{
    /// <summary>Builds the Avalonia application used by headless xUnit tests.</summary>
    /// <returns>The configured Avalonia application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
