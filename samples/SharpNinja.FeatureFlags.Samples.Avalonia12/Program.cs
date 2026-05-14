using Avalonia;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>Desktop entry point for the SharpNinja Feature Flags Avalonia 12 sample.</summary>
public static class Program
{
    /// <summary>Starts the Avalonia desktop application.</summary>
    /// <param name="args">Command-line arguments supplied by the host.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Builds the Avalonia application host.</summary>
    /// <returns>The configured Avalonia application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
