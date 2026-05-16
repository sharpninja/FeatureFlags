using SharpNinja.FeatureFlags.Abstractions;
using SharpNinja.FeatureFlags.Experimentation;
using Xunit;

namespace SharpNinja.FeatureFlags.Experimentation.Tests;

/// <summary>
/// Tests for <see cref="ExperimentAssigner"/> covering deterministic bucketing,
/// weight distribution, and flag-gate ineligibility behavior.
/// </summary>
public sealed class ExperimentAssignerTests
{
    private static ExperimentDefinition MakeDefinition(string expId = "exp-1", string flagKey = "flag-a", double w1 = 1.0, double w2 = 1.0)
    {
        return new ExperimentDefinition(
            expId,
            flagKey,
            new List<ExperimentVariant>
            {
                new ExperimentVariant("control", w1),
                new ExperimentVariant("treatment", w2),
            });
    }

    /// <summary>
    /// The same subjectId must always receive the same variant for a given experiment,
    /// ensuring deterministic bucketing across repeated calls.
    /// </summary>
    [Fact]
    public void AssignReturnsDeterministicVariantForSameSubject()
    {
        var client = new StubFeatureClient(enabled: true);
        var assigner = new ExperimentAssigner(client, sink: null);
        var definition = MakeDefinition();

        ExperimentAssignment first = assigner.Assign(definition, "user-123");
        ExperimentAssignment second = assigner.Assign(definition, "user-123");
        ExperimentAssignment third = assigner.Assign(definition, "user-123");

        Assert.Equal(first.VariantKey, second.VariantKey);
        Assert.Equal(first.VariantKey, third.VariantKey);
    }

    /// <summary>
    /// With equal weights (50/50), assigning 1000 distinct subjects should distribute
    /// within 45%/55% for each variant (±5% tolerance).
    /// </summary>
    [Fact]
    public void AssignDistributesSubjectsApproximatelyByWeight()
    {
        var client = new StubFeatureClient(enabled: true);
        var assigner = new ExperimentAssigner(client, sink: null);
        var definition = MakeDefinition();

        int controlCount = 0;
        int treatmentCount = 0;
        const int total = 1000;

        for (int i = 0; i < total; i++)
        {
            ExperimentAssignment assignment = assigner.Assign(definition, $"user-{i}");
            if (assignment.VariantKey == "control")
            {
                controlCount++;
            }
            else
            {
                treatmentCount++;
            }
        }

        double controlRatio = (double)controlCount / total;
        double treatmentRatio = (double)treatmentCount / total;

        Assert.InRange(controlRatio, 0.45, 0.55);
        Assert.InRange(treatmentRatio, 0.45, 0.55);
    }

    /// <summary>
    /// When the feature flag evaluates to false (disabled), the assignment must
    /// return <see cref="ExperimentAssignment.IsEligible"/> = false.
    /// </summary>
    [Fact]
    public void AssignReturnsIneligibleWhenFlagDisabled()
    {
        var client = new StubFeatureClient(enabled: false);
        var assigner = new ExperimentAssigner(client, sink: null);
        var definition = MakeDefinition();

        ExperimentAssignment assignment = assigner.Assign(definition, "user-999");

        Assert.False(assignment.IsEligible);
    }

    /// <summary>
    /// When the flag gate is disabled and the subject is ineligible, the assignment
    /// must return the first (control) variant key, not a random treatment.
    /// </summary>
    [Fact]
    public void AssignReturnsControlVariantWhenIneligible()
    {
        var client = new StubFeatureClient(enabled: false);
        var assigner = new ExperimentAssigner(client, sink: null);
        var definition = MakeDefinition();

        ExperimentAssignment assignment = assigner.Assign(definition, "user-999");

        Assert.Equal("control", assignment.VariantKey);
    }
}
