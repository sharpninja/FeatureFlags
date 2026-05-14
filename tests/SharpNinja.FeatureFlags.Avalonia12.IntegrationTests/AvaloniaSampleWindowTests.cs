using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using SharpNinja.FeatureFlags.Samples.Avalonia12;
using Xunit;

namespace SharpNinja.FeatureFlags.Avalonia12.IntegrationTests;

/// <summary>Rendered-window integration tests for the Avalonia 12 sample.</summary>
public sealed class AvaloniaSampleWindowTests
{
    /// <summary>Verifies that the sample window data-binds every expected display text.</summary>
    [Fact]
    public void MainWindowRendersEveryExpectedDisplayText()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(AvaloniaTestApplication));

        session.Dispatch(
            () =>
            {
                MainWindow window = new();

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

                    string[] renderedText = window.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Select(textBlock => textBlock.Text)
                        .OfType<string>()
                        .ToArray();

                    foreach (AvaloniaSampleScenarioOutput expected in AvaloniaSampleScenarioRunner.GetExpectedOutputs())
                    {
                        Assert.Contains(expected.DisplayText, renderedText);
                    }
                }
                finally
                {
                    window.Close();
                }
            },
            CancellationToken.None);
    }
}
