using Avalonia.Controls;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>Main window that renders TEST-AVALONIA-SAMPLE-001 feature flag scenario outputs.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the TEST-AVALONIA-SAMPLE-001 sample window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(AvaloniaSampleScenarioRunner.GetOutputs());
    }
}
