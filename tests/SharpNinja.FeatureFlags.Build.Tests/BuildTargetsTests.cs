using System.Xml.Linq;
using Xunit;

namespace SharpNinja.FeatureFlags.Build.Tests;

/// <summary>FR-2 TR-1 TR-4 TR-6 TR-8 TR-11 tests for the build-transitive MSBuild integration surface.</summary>
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
            "SharpNinja.FeatureFlags.Build",
            "buildTransitive",
            "SharpNinja.FeatureFlags.Build.targets"));

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

        Assert.Contains("ValidateSharpNinjaFeatureFlagBuildProperties", targetNames);
        Assert.Contains("ValidateSharpNinjaFeatureFlagManifest", targetNames);
    }

    /// <summary>The canonical manifest is exposed to generators and embedded for runtime loading.</summary>
    [Fact]
    public void ManifestIsAdditionalFileAndEmbeddedResource()
    {
        var document = XDocument.Load(TargetsPath);
        var additionalFile = document.Descendants("AdditionalFiles")
            .Single(element => element.Attribute("Include")?.Value == "$(SharpNinjaFeatureFlagsManifest)");
        var embeddedResource = document.Descendants("EmbeddedResource")
            .Single(element => element.Attribute("Include")?.Value == "$(SharpNinjaFeatureFlagsManifest)");

        Assert.Equal("$(SharpNinjaFeatureFlagsManifest)", additionalFile.Attribute("Include")?.Value);
        Assert.Equal("$(SharpNinjaFeatureFlagsManifest)", embeddedResource.Attribute("Include")?.Value);
        Assert.Equal(
            "$(SharpNinjaFeatureFlagsManifestResourceName)",
            embeddedResource.Attribute("LogicalName")?.Value);
        Assert.Equal("Manifest", additionalFile.Element("SharpNinjaFeatureFlagsKind")?.Value);
        Assert.Equal("$(ProductId)", additionalFile.Element("ProductId")?.Value);
        Assert.Equal("$(ReleaseId)", additionalFile.Element("ReleaseId")?.Value);
        Assert.Equal("$(SharpNinjaFeatureFlagsManifestResourceName)", additionalFile.Element("ManifestResourceName")?.Value);
        Assert.Equal("$(SharpNinjaFeatureFlagsPublicKeyResourceName)", additionalFile.Element("PublicKeyResourceName")?.Value);
    }

    /// <summary>The Ed25519 public key is exposed to generators and embedded for runtime verification.</summary>
    [Fact]
    public void PublicKeyIsAdditionalFileAndEmbeddedResource()
    {
        var document = XDocument.Load(TargetsPath);
        var additionalFile = document.Descendants("AdditionalFiles")
            .Single(element => element.Attribute("Include")?.Value == "$(SharpNinjaFeatureFlagsPublicKey)");
        var embeddedResource = document.Descendants("EmbeddedResource")
            .Single(element => element.Attribute("Include")?.Value == "$(SharpNinjaFeatureFlagsPublicKey)");

        Assert.Equal("PublicKey", additionalFile.Element("SharpNinjaFeatureFlagsKind")?.Value);
        Assert.Equal("$(SharpNinjaFeatureFlagsPublicKeyResourceName)", additionalFile.Element("PublicKeyResourceName")?.Value);
        Assert.Equal("$(SharpNinjaFeatureFlagsPublicKeyResourceName)", embeddedResource.Attribute("LogicalName")?.Value);
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
        Assert.Contains(
            "'$(SharpNinjaFeatureFlagsRequirePublicKey)' == 'true' and !Exists('$(SharpNinjaFeatureFlagsPublicKey)')",
            errors);
    }

    /// <summary>Build identity and resource names are stamped as assembly metadata for the SDK/runtime handoff.</summary>
    [Fact]
    public void AssemblyMetadataAttributesStampBuildIdentityAndResources()
    {
        var document = XDocument.Load(TargetsPath);
        var metadataNames = document.Descendants("AssemblyAttribute")
            .Where(element => element.Attribute("Include")?.Value == "System.Reflection.AssemblyMetadataAttribute")
            .Select(element => element.Element("_Parameter1")?.Value)
            .ToArray();

        Assert.Contains("SharpNinja.FeatureFlags.ProductId", metadataNames);
        Assert.Contains("SharpNinja.FeatureFlags.ReleaseId", metadataNames);
        Assert.Contains("SharpNinja.FeatureFlags.ManifestResourceName", metadataNames);
        Assert.Contains("SharpNinja.FeatureFlags.PublicKeyResourceName", metadataNames);
    }

    /// <summary>The MSBuild validation command forwards identity, schema, public key, and binding diagnostics inputs.</summary>
    [Fact]
    public void ValidateCommandPassesV1ValidationArguments()
    {
        var document = XDocument.Load(TargetsPath);
        var command = document.Descendants("Exec").Single().Attribute("Command")?.Value;

        Assert.Contains("--product-id \"$(ProductId)\"", command, StringComparison.Ordinal);
        Assert.Contains("--release-id \"$(ReleaseId)\"", command, StringComparison.Ordinal);
        Assert.Contains("--schema-version \"$(SharpNinjaFeatureFlagsSchemaVersion)\"", command, StringComparison.Ordinal);
        Assert.Contains("$(SharpNinjaFeatureFlagsValidatePublicKeyArgs)", command, StringComparison.Ordinal);
        Assert.Contains("--generated-bindings", command, StringComparison.Ordinal);
    }

    /// <summary>The optional generated registration source target emits a zero-argument AddSharpNinjaFeatureFlags overload.</summary>
    [Fact]
    public void GeneratedRegistrationTargetEmitsAddSharpNinjaFeatureFlagsOverload()
    {
        var document = XDocument.Load(TargetsPath);
        var target = document.Descendants("Target")
            .Single(element => element.Attribute("Name")?.Value == "GenerateSharpNinjaFeatureFlagsRegistrationSource");
        var generatedLines = target.Descendants("_SharpNinjaFeatureFlagsGeneratedSourceLine")
            .Select(element => element.Attribute("Include")?.Value)
            .ToArray();

        Assert.Contains(
            "    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddSharpNinjaFeatureFlags(this Microsoft.Extensions.DependencyInjection.IServiceCollection services)",
            generatedLines);
        Assert.Contains(
            "        return SharpNinjaFeatureFlagServiceCollectionExtensions.AddSharpNinjaFeatureFlags(services, options.Validate(), ReadEmbeddedResource(ManifestResourceName));",
            generatedLines);
    }
}
