using System.Collections.ObjectModel;

namespace SharpNinja.FeatureFlags.Samples.Avalonia12;

/// <summary>View model for the TEST-AVALONIA-SAMPLE-001 Avalonia sample main window.</summary>
public sealed class MainWindowViewModel
{
    /// <summary>Creates a TEST-AVALONIA-SAMPLE-001 main window view model.</summary>
    /// <param name="scenarioOutputs">Deterministic scenario outputs to render.</param>
    public MainWindowViewModel(IEnumerable<AvaloniaSampleScenarioOutput> scenarioOutputs)
    {
        ArgumentNullException.ThrowIfNull(scenarioOutputs);

        ScenarioOutputs = new ReadOnlyObservableCollection<AvaloniaSampleScenarioOutput>(
            new ObservableCollection<AvaloniaSampleScenarioOutput>(scenarioOutputs));
    }

    /// <summary>Gets the TEST-AVALONIA-SAMPLE-001 scenario outputs displayed by the window.</summary>
    public ReadOnlyObservableCollection<AvaloniaSampleScenarioOutput> ScenarioOutputs { get; }
}
