using SharpNinja.FeatureFlags.Abstractions;
using Xunit;

namespace SharpNinja.FeatureFlags.Abstractions.Tests;

/// <summary>FR-7 FR-12 tests for FeatureFlagDisabledException.</summary>
public sealed class FeatureFlagDisabledExceptionTests
{
    /// <summary>FR-7 Single-arg constructor stores flag key and sets default message.</summary>
    [Fact]
    public void SingleArgConstructorStoresFlagKey()
    {
        var ex = new FeatureFlagDisabledException("dashboard.enabled");

        Assert.Equal("dashboard.enabled", ex.FlagKey);
        Assert.Contains("dashboard.enabled", ex.Message);
    }

    /// <summary>FR-7 Two-arg constructor stores flag key and custom message.</summary>
    [Fact]
    public void TwoArgConstructorStoresFlagKeyAndMessage()
    {
        var ex = new FeatureFlagDisabledException("reports.view", "Reports are not available.");

        Assert.Equal("reports.view", ex.FlagKey);
        Assert.Equal("Reports are not available.", ex.Message);
    }

    /// <summary>FR-7 Three-arg constructor chains inner exception.</summary>
    [Fact]
    public void ThreeArgConstructorChainsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new FeatureFlagDisabledException("kill.switch", "Killed.", inner);

        Assert.Equal("kill.switch", ex.FlagKey);
        Assert.Same(inner, ex.InnerException);
    }

    /// <summary>FR-7 Null flag key throws ArgumentNullException.</summary>
    [Fact]
    public void NullFlagKeyThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new FeatureFlagDisabledException(null!));
    }

    /// <summary>FR-7 Exception is assignable to InvalidOperationException.</summary>
    [Fact]
    public void IsInvalidOperationException()
    {
        var ex = new FeatureFlagDisabledException("x");

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }
}
