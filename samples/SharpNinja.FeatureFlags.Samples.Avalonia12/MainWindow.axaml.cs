using Avalonia.Controls;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>Main window that renders the deterministic feature flag scenario outputs.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the main sample window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(AvaloniaSampleScenarioRunner.GetOutputs());
    }
}
