using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>Avalonia application for the TEST-AVALONIA-SAMPLE-001 sample.</summary>
public sealed partial class App : Application
{
    /// <summary>Loads XAML resources for the TEST-AVALONIA-SAMPLE-001 application.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Creates the main window for the TEST-AVALONIA-SAMPLE-001 desktop lifetime.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
