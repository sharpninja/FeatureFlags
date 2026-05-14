using Xunit;

namespace SharpNinja.FeatureFlags.Build.Tests;

/// <summary>Tests for Docker hosting artifacts that compose the v1 service topology.</summary>
public sealed class DockerHostingArtifactsTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string ComposePath = Path.Combine(RepositoryRoot, "docker-compose.yml");

    /// <summary>The compose file defines the admin plane, distribution service, and v1 database services.</summary>
    [Fact]
    public void ComposeDefinesApplicationAndDatabaseServices()
    {
        var compose = File.ReadAllText(ComposePath);

        Assert.Contains("admin:", compose, StringComparison.Ordinal);
        Assert.Contains("distribution:", compose, StringComparison.Ordinal);
        Assert.Contains("postgres:", compose, StringComparison.Ordinal);
        Assert.Contains("sqlserver:", compose, StringComparison.Ordinal);
        Assert.Contains("dockerfile: src/SharpNinja.FeatureFlags.Admin/Dockerfile", compose, StringComparison.Ordinal);
        Assert.Contains("dockerfile: src/SharpNinja.FeatureFlags.Distribution/Dockerfile", compose, StringComparison.Ordinal);
        Assert.Contains("image: postgres:16", compose, StringComparison.Ordinal);
        Assert.Contains("image: mcr.microsoft.com/mssql/server:2022-latest", compose, StringComparison.Ordinal);
    }

    /// <summary>The compose file records the decided v1 hosting scope as explicit service configuration.</summary>
    [Fact]
    public void ComposeCapturesDecidedV1Scope()
    {
        var compose = File.ReadAllText(ComposePath);

        Assert.Contains("FeatureFlags__Products__0: TruckMate", compose, StringComparison.Ordinal);
        Assert.Contains("FeatureFlags__Products__1: DriverMate", compose, StringComparison.Ordinal);
        Assert.Contains("FeatureFlags__ReleaseLineage__Mode: SemVerChannelBuild", compose, StringComparison.Ordinal);
        Assert.Contains("FeatureFlags__Environments__AllowCustom: \"true\"", compose, StringComparison.Ordinal);
        Assert.Contains("FeatureFlags__ExposureRetention__UserDefined: \"true\"", compose, StringComparison.Ordinal);
        Assert.Contains("FeatureFlags__Tenancy__Mode: MultiTenant", compose, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__Postgres:", compose, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__SqlServer:", compose, StringComparison.Ordinal);
    }

    /// <summary>The service Dockerfiles use .NET 10 SDK and ASP.NET runtime stages.</summary>
    /// <param name="dockerfilePath">Repository-relative Dockerfile path.</param>
    /// <param name="projectPath">Repository-relative project path that is restored and published.</param>
    /// <param name="entrypoint">Runtime DLL entrypoint.</param>
    [Theory]
    [InlineData(
        "src/SharpNinja.FeatureFlags.Admin/Dockerfile",
        "src/SharpNinja.FeatureFlags.Admin/SharpNinja.FeatureFlags.Admin.csproj",
        "SharpNinja.FeatureFlags.Admin.dll")]
    [InlineData(
        "src/SharpNinja.FeatureFlags.Distribution/Dockerfile",
        "src/SharpNinja.FeatureFlags.Distribution/SharpNinja.FeatureFlags.Distribution.csproj",
        "SharpNinja.FeatureFlags.Distribution.dll")]
    public void DockerfilesPublishNet10WebProjects(string dockerfilePath, string projectPath, string entrypoint)
    {
        var dockerfile = File.ReadAllText(Path.Combine(RepositoryRoot, dockerfilePath));

        Assert.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build", dockerfile, StringComparison.Ordinal);
        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime", dockerfile, StringComparison.Ordinal);
        Assert.Contains($"RUN dotnet restore {projectPath}", dockerfile, StringComparison.Ordinal);
        Assert.Contains($"RUN dotnet publish {projectPath}", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains($"ENTRYPOINT [\"dotnet\", \"{entrypoint}\"]", dockerfile, StringComparison.Ordinal);
    }
}
