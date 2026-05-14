using Avalonia;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>Desktop entry point for the TEST-AVALONIA-SAMPLE-001 Avalonia 12 sample.</summary>
public static class Program
{
    /// <summary>Starts the TEST-AVALONIA-SAMPLE-001 Avalonia desktop application.</summary>
    /// <param name="args">Command-line arguments supplied by the host.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Builds the TEST-AVALONIA-SAMPLE-001 Avalonia application host.</summary>
    /// <returns>The configured Avalonia application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
