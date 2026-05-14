using System.Xml.Linq;
using Xunit;

namespace Byrd.FeatureFlags.Build.Tests;

/// <summary>Tests for the build-transitive MSBuild integration surface.</summary>
public sealed class BuildTargetsTests
{
    private static readonly string TargetsPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Byrd.FeatureFlags.Build",
            "buildTransitive",
            "Byrd.FeatureFlags.Build.targets"));

    /// <summary>The package exposes one build-transitive targets file.</summary>
    [Fact]
    public void TargetsFileExists()
    {
        Assert.True(File.Exists(TargetsPath), $"Missing targets file: {TargetsPath}");
    }

    /// <summary>The targets file defines the expected validation targets.</summary>
    [Fact]
    public void TargetsFileDefinesValidationTargets()
    {
        var document = XDocument.Load(TargetsPath);
        var targetNames = document.Root!
            .Elements("Target")
            .Select(element => element.Attribute("Name")?.Value)
            .ToArray();

        Assert.Contains("ValidateByrdFeatureFlagBuildProperties", targetNames);
        Assert.Contains("ValidateByrdFeatureFlagManifest", targetNames);
    }

    /// <summary>The canonical manifest is exposed to generators and embedded for runtime loading.</summary>
    [Fact]
    public void ManifestIsAdditionalFileAndEmbeddedResource()
    {
        var document = XDocument.Load(TargetsPath);
        var additionalFile = document.Descendants("AdditionalFiles").Single();
        var embeddedResource = document.Descendants("EmbeddedResource").Single();

        Assert.Equal("$(ByrdFeatureFlagsManifest)", additionalFile.Attribute("Include")?.Value);
        Assert.Equal("$(ByrdFeatureFlagsManifest)", embeddedResource.Attribute("Include")?.Value);
        Assert.Equal(
            "Byrd.FeatureFlags.BundledManifest.json",
            embeddedResource.Attribute("LogicalName")?.Value);
    }

    /// <summary>Product and release identity are required before manifest validation can run.</summary>
    [Fact]
    public void ProductAndReleaseErrorsAreDeclared()
    {
        var document = XDocument.Load(TargetsPath);
        var errors = document.Descendants("Error")
            .Select(element => element.Attribute("Condition")?.Value)
            .ToArray();

        Assert.Contains("'$(ProductId)' == ''", errors);
        Assert.Contains("'$(ReleaseId)' == ''", errors);
    }
}
