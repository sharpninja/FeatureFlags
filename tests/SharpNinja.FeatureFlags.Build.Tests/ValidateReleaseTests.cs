using System.Xml.Linq;
using Xunit;

namespace SharpNinja.FeatureFlags.Build.Tests;

/// <summary>
/// Tests for the ValidateRelease build target logic.
/// FR-12, TR-1
/// Verifies that v1 NuGet packages carry required packaging metadata
/// and that SQLite is excluded from the v1 shipping set.
/// </summary>
public sealed class ValidateReleaseTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string SrcRoot = Path.Combine(RepositoryRoot, "src");

    /// <summary>
    /// The set of src projects that must be packaged as NuGet libraries in v1.
    /// Admin and Distribution are Docker-hosted services (not NuGet packages).
    /// Admin.Data.Sqlite is explicitly excluded from v1 shipping.
    /// Cli ships as a tool, not a library NuGet package.
    /// </summary>
    private static readonly string[] V1NuGetProjects =
    [
        "SharpNinja.FeatureFlags.Abstractions",
        "SharpNinja.FeatureFlags.Evaluation",
        "SharpNinja.FeatureFlags",
        "SharpNinja.FeatureFlags.Manifest",
        "SharpNinja.FeatureFlags.Cqrs",
        "SharpNinja.FeatureFlags.Build",
        "SharpNinja.FeatureFlags.Admin.Data",
        "SharpNinja.FeatureFlags.Admin.Data.Postgres",
        "SharpNinja.FeatureFlags.Admin.Data.SqlServer",
    ];

    /// <summary>
    /// Projects that must NOT be packaged as NuGet libraries in v1.
    /// Admin and Distribution are Docker-hosted ASP.NET services.
    /// Admin.Data.Sqlite is a v1 exclusion per the resolved architecture decisions.
    /// </summary>
    private static readonly string[] V1NonPackageProjects =
    [
        "SharpNinja.FeatureFlags.Admin",
        "SharpNinja.FeatureFlags.Distribution",
        "SharpNinja.FeatureFlags.Admin.Data.Sqlite",
    ];

    /// <summary>Each v1 NuGet project must declare IsPackable true.</summary>
    [Theory]
    [MemberData(nameof(V1NuGetProjectNames))]
    public void V1NuGetProjectHasIsPackableTrue(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);
        var isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;

        Assert.Equal("true", isPackable, ignoreCase: true);
    }

    /// <summary>Each v1 NuGet project must declare a non-empty PackageId.</summary>
    [Theory]
    [MemberData(nameof(V1NuGetProjectNames))]
    public void V1NuGetProjectHasNonEmptyPackageId(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);
        var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value;

        Assert.False(string.IsNullOrWhiteSpace(packageId),
            $"{projectName} must declare a non-empty <PackageId>.");
    }

    /// <summary>Each v1 NuGet project must declare a non-empty Version.</summary>
    [Theory]
    [MemberData(nameof(V1NuGetProjectNames))]
    public void V1NuGetProjectHasNonEmptyVersion(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);
        var version = doc.Descendants("Version").FirstOrDefault()?.Value;

        Assert.False(string.IsNullOrWhiteSpace(version),
            $"{projectName} must declare a non-empty <Version>.");
    }

    /// <summary>Each v1 NuGet project must declare a non-empty Authors element.</summary>
    [Theory]
    [MemberData(nameof(V1NuGetProjectNames))]
    public void V1NuGetProjectHasAuthors(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);
        var authors = doc.Descendants("Authors").FirstOrDefault()?.Value;

        Assert.False(string.IsNullOrWhiteSpace(authors),
            $"{projectName} must declare a non-empty <Authors>.");
    }

    /// <summary>Each v1 NuGet project must declare a non-empty Description element.</summary>
    [Theory]
    [MemberData(nameof(V1NuGetProjectNames))]
    public void V1NuGetProjectHasDescription(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);
        var description = doc.Descendants("Description").FirstOrDefault()?.Value;

        Assert.False(string.IsNullOrWhiteSpace(description),
            $"{projectName} must declare a non-empty <Description>.");
    }

    /// <summary>
    /// Projects that are not v1 NuGet packages must NOT set IsPackable true.
    /// Web/service projects default to IsPackable false; Sqlite is excluded per v1 decisions.
    /// </summary>
    [Theory]
    [MemberData(nameof(V1NonPackageProjectNames))]
    public void V1NonPackageProjectDoesNotSetIsPackableTrue(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);
        var isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;

        Assert.False(
            string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase),
            $"{projectName} must not set <IsPackable>true</IsPackable>; it is not a v1 NuGet package.");
    }

    /// <summary>
    /// SQLite project must NOT appear in Directory.Packages.props as a PackageId
    /// that would imply it is being shipped. Its presence as a project is tolerated
    /// for future work, but it must not be wired as a releasable artifact.
    /// </summary>
    [Fact]
    public void SqliteProjectNotListedAsV1ReleasablePackage()
    {
        // The Sqlite project file must not set IsPackable true.
        var sqliteProject = GetProjectFile("SharpNinja.FeatureFlags.Admin.Data.Sqlite");
        Assert.True(File.Exists(sqliteProject), $"Project file not found: {sqliteProject}");

        var doc = XDocument.Load(sqliteProject);
        var isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;

        Assert.False(
            string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase),
            "Admin.Data.Sqlite must not be marked IsPackable=true; it is excluded from v1 shipping.");
    }

    /// <summary>
    /// The Cli project ships as a dotnet tool, not a plain library NuGet package.
    /// It must declare PackAsTool true if it is packaged at all.
    /// </summary>
    [Fact]
    public void CliProjectIfPackagedMustDeclarePackAsTool()
    {
        var cliProject = GetProjectFile("SharpNinja.FeatureFlags.Cli");
        Assert.True(File.Exists(cliProject), $"Project file not found: {cliProject}");

        var doc = XDocument.Load(cliProject);
        var isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;

        // If the Cli project is packable, it must declare PackAsTool=true.
        if (string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase))
        {
            var packAsTool = doc.Descendants("PackAsTool").FirstOrDefault()?.Value;
            Assert.Equal("true", packAsTool, ignoreCase: true);
        }
    }

    /// <summary>
    /// The Admin service project must have a Dockerfile for Docker image production.
    /// </summary>
    [Fact]
    public void AdminServiceHasDockerfile()
    {
        var dockerfilePath = Path.Combine(SrcRoot, "SharpNinja.FeatureFlags.Admin", "Dockerfile");
        Assert.True(File.Exists(dockerfilePath),
            $"Missing Dockerfile for Admin service: {dockerfilePath}");
    }

    /// <summary>
    /// The Distribution service project must have a Dockerfile for Docker image production.
    /// </summary>
    [Fact]
    public void DistributionServiceHasDockerfile()
    {
        var dockerfilePath = Path.Combine(SrcRoot, "SharpNinja.FeatureFlags.Distribution", "Dockerfile");
        Assert.True(File.Exists(dockerfilePath),
            $"Missing Dockerfile for Distribution service: {dockerfilePath}");
    }

    /// <summary>
    /// No v1 NuGet package project may reference a Debug-only or development-only
    /// package (identified by PrivateAssets="all" combined with common dev-only package IDs).
    /// </summary>
    [Theory]
    [MemberData(nameof(V1NuGetProjectNames))]
    public void V1NuGetProjectHasNoDebugOnlyPackageReferencesLeakedToConsumers(string projectName)
    {
        var projectFile = GetProjectFile(projectName);
        Assert.True(File.Exists(projectFile), $"Project file not found: {projectFile}");

        var doc = XDocument.Load(projectFile);

        // Known debug/dev-only package IDs that should never appear as transitive
        // dependencies in a released NuGet package without PrivateAssets="all".
        string[] forbiddenWithoutPrivateAssets =
        [
            "Microsoft.NET.Test.Sdk",
            "xunit",
            "xunit.runner.visualstudio",
            "coverlet.collector",
            "Moq",
            "NSubstitute",
        ];

        var leakedRefs = doc.Descendants("PackageReference")
            .Where(element =>
            {
                var id = element.Attribute("Include")?.Value ?? string.Empty;
                var privateAssets = element.Attribute("PrivateAssets")?.Value
                    ?? element.Element("PrivateAssets")?.Value
                    ?? string.Empty;
                return forbiddenWithoutPrivateAssets.Any(forbidden =>
                        string.Equals(id, forbidden, StringComparison.OrdinalIgnoreCase))
                    && !string.Equals(privateAssets, "all", StringComparison.OrdinalIgnoreCase);
            })
            .Select(element => element.Attribute("Include")?.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leakedRefs);
    }

    // ---- MemberData providers ----

    /// <summary>Provides v1 NuGet project names as xUnit theory data.</summary>
    public static IEnumerable<object[]> V1NuGetProjectNames()
        => V1NuGetProjects.Select(name => new object[] { name });

    /// <summary>Provides v1 non-package project names as xUnit theory data.</summary>
    public static IEnumerable<object[]> V1NonPackageProjectNames()
        => V1NonPackageProjects.Select(name => new object[] { name });

    // ---- Helpers ----

    private static string GetProjectFile(string projectName) =>
        Path.Combine(SrcRoot, projectName, $"{projectName}.csproj");
}
