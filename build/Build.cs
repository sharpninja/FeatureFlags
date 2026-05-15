using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>Build entrypoint for Phase 0 repository validation.</summary>
sealed class Build : NukeBuild
{
    /// <summary>Runs the requested target.</summary>
    public static int Main() => Execute<Build>(x => x.Compile);

    /// <summary>Build configuration.</summary>
    [Parameter("Build configuration")]
    readonly string Configuration = "Release";

    static AbsolutePath Solution => RootDirectory / "sharpninja-feature-flags.sln";

    static readonly Regex PublicDeclarationPattern = new(
        @"^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|record|interface|enum|struct)\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    static readonly Regex RequirementIdPattern = new(
        @"\b(?:(?:FR|TR)-\d+|TEST-[A-Z0-9-]+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Compiles the solution.</summary>
    Target Compile => _ => _
        .Executes(() => DotNetBuild(settings => settings
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)));

    /// <summary>Runs test projects, excluding integration-test naming patterns.</summary>
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTest(settings => settings
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .EnableNoBuild()
            .SetFilter("FullyQualifiedName!~IntegrationTests")));

    /// <summary>Validates committed v1 configuration decisions and hosting/provider artifacts.</summary>
    Target ValidateConfig => _ => _
        .DependsOn(Compile)
        .Executes(ValidateConfiguration);

    /// <summary>Validates implementation source files carry requirement traceability identifiers.</summary>
    Target ValidateTraceability => _ => _
        .DependsOn(Compile)
        .Executes(ValidateTraceabilitySummaries);

    /// <summary>Validates that all v1 release artifacts are correctly configured for NuGet packaging and Docker image production.</summary>
    Target ValidateRelease => _ => _
        .DependsOn(Compile)
        .Executes(ValidateReleaseArtifacts);

    /// <summary>
    /// Projects in src/ that must ship as NuGet library packages in v1.
    /// Admin and Distribution are Docker-hosted services, not library packages.
    /// Admin.Data.Sqlite is excluded per the resolved v1 provider decision.
    /// Cli ships as a dotnet tool (PackAsTool), validated separately.
    /// </summary>
    static readonly string[] V1NuGetProjects =
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
    /// Projects that are Docker-hosted services. Each must have a Dockerfile alongside its project file.
    /// </summary>
    static readonly string[] V1DockerProjects =
    [
        "SharpNinja.FeatureFlags.Admin",
        "SharpNinja.FeatureFlags.Distribution",
    ];

    /// <summary>
    /// Test-only and dev-only package IDs that must never appear as non-private references
    /// in a v1 NuGet release package.
    /// </summary>
    static readonly string[] ForbiddenReleasePackages =
    [
        "Microsoft.NET.Test.Sdk",
        "xunit",
        "xunit.runner.visualstudio",
        "coverlet.collector",
        "Moq",
        "NSubstitute",
    ];

    static void ValidateReleaseArtifacts()
    {
        List<string> violations = [];

        ValidateV1NuGetPackagingMetadata(violations);
        ValidateV1DockerArtifacts(violations);
        ValidateNoDebugPackagesLeakedIntoReleaseProjects(violations);
        ValidateSqliteNotPackagedAsV1NuGet(violations);
        ValidateCliPackedAsTool(violations);

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                string.Concat(
                    "Release validation failed:",
                    Environment.NewLine,
                    string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal).Select(v => string.Concat(" - ", v)))));
        }

        Serilog.Log.Information(
            "Release validation passed: {NuGetCount} NuGet packages and {DockerCount} Docker service projects verified.",
            V1NuGetProjects.Length,
            V1DockerProjects.Length);
    }

    static void ValidateV1NuGetPackagingMetadata(List<string> violations)
    {
        foreach (string projectName in V1NuGetProjects)
        {
            string projectFile = RootDirectory / "src" / projectName / $"{projectName}.csproj";
            if (!File.Exists(projectFile))
            {
                violations.Add($"Missing v1 NuGet project file: src/{projectName}/{projectName}.csproj");
                continue;
            }

            XDocument doc = XDocument.Load(projectFile);
            string relativePath = Path.GetRelativePath(RootDirectory, projectFile);

            string? isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;
            if (!string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{relativePath}: must set <IsPackable>true</IsPackable> for NuGet release.");
            }

            string? packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                violations.Add($"{relativePath}: must set a non-empty <PackageId>.");
            }

            string? version = doc.Descendants("Version").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(version))
            {
                violations.Add($"{relativePath}: must set a non-empty <Version>.");
            }

            string? authors = doc.Descendants("Authors").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(authors))
            {
                violations.Add($"{relativePath}: must set a non-empty <Authors>.");
            }

            string? description = doc.Descendants("Description").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(description))
            {
                violations.Add($"{relativePath}: must set a non-empty <Description>.");
            }
        }
    }

    static void ValidateV1DockerArtifacts(List<string> violations)
    {
        foreach (string projectName in V1DockerProjects)
        {
            string dockerfilePath = RootDirectory / "src" / projectName / "Dockerfile";
            if (!File.Exists(dockerfilePath))
            {
                violations.Add($"Missing Dockerfile for Docker-hosted service: src/{projectName}/Dockerfile");
            }
        }
    }

    static void ValidateNoDebugPackagesLeakedIntoReleaseProjects(List<string> violations)
    {
        foreach (string projectName in V1NuGetProjects)
        {
            string projectFile = RootDirectory / "src" / projectName / $"{projectName}.csproj";
            if (!File.Exists(projectFile))
            {
                continue;
            }

            XDocument doc = XDocument.Load(projectFile);
            string relativePath = Path.GetRelativePath(RootDirectory, projectFile);

            string[] leakedRefs = doc.Descendants("PackageReference")
                .Where(element =>
                {
                    string id = element.Attribute("Include")?.Value ?? string.Empty;
                    string privateAssets = element.Attribute("PrivateAssets")?.Value
                        ?? element.Element("PrivateAssets")?.Value
                        ?? string.Empty;
                    return ForbiddenReleasePackages.Any(forbidden =>
                            string.Equals(id, forbidden, StringComparison.OrdinalIgnoreCase))
                        && !string.Equals(privateAssets, "all", StringComparison.OrdinalIgnoreCase);
                })
                .Select(element => element.Attribute("Include")?.Value ?? "<unknown>")
                .Order(StringComparer.Ordinal)
                .ToArray();

            foreach (string leaked in leakedRefs)
            {
                violations.Add($"{relativePath}: dev/test-only package '{leaked}' must use PrivateAssets=\"all\" or be removed from release projects.");
            }
        }
    }

    static void ValidateSqliteNotPackagedAsV1NuGet(List<string> violations)
    {
        string sqliteProject = RootDirectory / "src" / "SharpNinja.FeatureFlags.Admin.Data.Sqlite" / "SharpNinja.FeatureFlags.Admin.Data.Sqlite.csproj";
        if (!File.Exists(sqliteProject))
        {
            return;
        }

        XDocument doc = XDocument.Load(sqliteProject);
        string? isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;
        if (string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("src/SharpNinja.FeatureFlags.Admin.Data.Sqlite: must not set <IsPackable>true</IsPackable>; SQLite is excluded from v1 NuGet shipping.");
        }
    }

    static void ValidateCliPackedAsTool(List<string> violations)
    {
        string cliProject = RootDirectory / "src" / "SharpNinja.FeatureFlags.Cli" / "SharpNinja.FeatureFlags.Cli.csproj";
        if (!File.Exists(cliProject))
        {
            return;
        }

        XDocument doc = XDocument.Load(cliProject);
        string? isPackable = doc.Descendants("IsPackable").FirstOrDefault()?.Value;
        if (!string.Equals(isPackable, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? packAsTool = doc.Descendants("PackAsTool").FirstOrDefault()?.Value;
        if (!string.Equals(packAsTool, "true", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("src/SharpNinja.FeatureFlags.Cli: is marked IsPackable=true but does not set <PackAsTool>true</PackAsTool>; the CLI must ship as a dotnet tool, not a plain library package.");
        }
    }

    static void ValidateTraceabilitySummaries()
    {
        string[] roots =
        [
            RootDirectory / "src",
            RootDirectory / "samples",
        ];

        List<string> violations = [];
        int checkedFiles = 0;

        foreach (string root in roots.Where(Directory.Exists))
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedOutput(file))
                {
                    continue;
                }

                string source = File.ReadAllText(file);
                if (!PublicDeclarationPattern.IsMatch(source))
                {
                    continue;
                }

                checkedFiles++;
                if (!RequirementIdPattern.IsMatch(source))
                {
                    violations.Add(Path.GetRelativePath(RootDirectory, file));
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                string.Concat(
                    "Traceability validation failed. Public implementation files must include an FR-*, TR-*, or TEST-* identifier in XML documentation:",
                    Environment.NewLine,
                    string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal).Select(path => string.Concat(" - ", path)))));
        }

        Serilog.Log.Information("Traceability validation passed for {FileCount} source files.", checkedFiles);
    }

    static bool IsGeneratedOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    static void ValidateConfiguration()
    {
        List<string> violations = [];

        ValidateResolvedV1Decisions(violations);
        ValidateCanonicalPlanningDocument(violations);
        ValidateDockerHostingArtifacts(violations);
        ValidateV1ProviderProjects(violations);
        ValidateSqliteExcludedFromV1ShippingPaths(violations);
        ValidateCentralPackageManagement(violations);

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                string.Concat(
                    "Configuration validation failed:",
                    Environment.NewLine,
                    string.Join(Environment.NewLine, violations.Order(StringComparer.Ordinal).Select(violation => string.Concat(" - ", violation)))));
        }

        Serilog.Log.Information("Configuration validation passed with {ValidationCount} checks.", 6);
    }

    static void ValidateResolvedV1Decisions(List<string> violations)
    {
        string openQuestionsPath = RootDirectory / "docs" / "Project" / "Open-Questions-v0.2.md";
        if (!File.Exists(openQuestionsPath))
        {
            violations.Add("Missing docs/Project/Open-Questions-v0.2.md.");
            return;
        }

        string source = File.ReadAllText(openQuestionsPath);
        string[] requiredFragments =
        [
            "Resolved 2026-05-14: v1 Products are TruckMate and DriverMate.",
            "Resolved 2026-05-14: use both strict semver per Product and channel-and-build lineage.",
            "Resolved 2026-05-14: environments beyond dev / staging / prod are custom-defined.",
            "Resolved 2026-05-14: host the admin plane and Distribution service with Docker.",
            "Resolved 2026-05-14: exposure-event data retention is user-definable.",
            "Resolved 2026-05-14: v1 database providers are PostgreSQL and SQL Server. SQLite is not a required v1 provider.",
            "Resolved 2026-05-14: multi-tenant deployment is in scope for v1.",
        ];

        RequireFragments("docs/Project/Open-Questions-v0.2.md", source, requiredFragments, violations);
    }

    static void ValidateDockerHostingArtifacts(List<string> violations)
    {
        string composePath = RootDirectory / "docker-compose.yml";
        string adminDockerfile = RootDirectory / "src" / "SharpNinja.FeatureFlags.Admin" / "Dockerfile";
        string distributionDockerfile = RootDirectory / "src" / "SharpNinja.FeatureFlags.Distribution" / "Dockerfile";

        RequireFile(composePath, violations);
        RequireFile(adminDockerfile, violations);
        RequireFile(distributionDockerfile, violations);

        if (File.Exists(composePath))
        {
            RequireFragments(
                "docker-compose.yml",
                File.ReadAllText(composePath),
                [
                    "admin",
                    "distribution",
                    "postgres",
                    "sqlserver",
                ],
                violations);
        }

        foreach (string dockerfile in new[] { adminDockerfile, distributionDockerfile }.Where(File.Exists))
        {
            RequireFragments(
                Path.GetRelativePath(RootDirectory, dockerfile),
                File.ReadAllText(dockerfile),
                [
                    "mcr.microsoft.com/dotnet/sdk:10.0",
                    "mcr.microsoft.com/dotnet/aspnet:10.0",
                    "dotnet publish",
                    "ENTRYPOINT",
                ],
                violations);
        }
    }

    static void ValidateCanonicalPlanningDocument(List<string> violations)
    {
        string planningPath = RootDirectory / "docs" / "Feature-Flag-Ecosystem-Planning-v0.1.md";
        if (!File.Exists(planningPath))
        {
            violations.Add("Missing docs/Feature-Flag-Ecosystem-Planning-v0.1.md.");
            return;
        }

        string source = File.ReadAllText(planningPath);

        RequireFragments(
            "docs/Feature-Flag-Ecosystem-Planning-v0.1.md",
            source,
            [
                "SharpNinja.FeatureFlags",
                "`net10.0`",
                "PostgreSQL and SQL Server",
                "Docker",
                "custom",
                "user-definable",
                "multi-tenant",
                "permanent",
            ],
            violations);

        RequireAbsentFragments(
            "docs/Feature-Flag-Ecosystem-Planning-v0.1.md",
            source,
            [
                "Byrd.FeatureFlags",
                "src/Byrd.FeatureFlags",
                "tests/Byrd.FeatureFlags",
                "net8.0",
                "ASP.NET Core 8",
                "PostgreSQL, SQL Server, and SQLite",
                "SQLite (the last primarily for local dev, integration tests, and small embedded deployments)",
                "\"Postgres\" | \"SqlServer\" | \"Sqlite\"",
            ],
            violations);
    }

    static void ValidateV1ProviderProjects(List<string> violations)
    {
        string[] requiredProviderProjects =
        [
            RootDirectory / "src" / "SharpNinja.FeatureFlags.Admin.Data.Postgres" / "SharpNinja.FeatureFlags.Admin.Data.Postgres.csproj",
            RootDirectory / "src" / "SharpNinja.FeatureFlags.Admin.Data.SqlServer" / "SharpNinja.FeatureFlags.Admin.Data.SqlServer.csproj",
        ];

        foreach (string providerProject in requiredProviderProjects)
        {
            RequireFile(providerProject, violations);
        }
    }

    static void ValidateSqliteExcludedFromV1ShippingPaths(List<string> violations)
    {
        string[] shippingPathFiles =
        [
            RootDirectory / "sharpninja-feature-flags.sln",
            RootDirectory / "tests" / "ArchitectureTests" / "ArchitectureTests.csproj",
        ];

        foreach (string shippingPathFile in shippingPathFiles)
        {
            if (!File.Exists(shippingPathFile))
            {
                continue;
            }

            RequireAbsentFragments(
                Path.GetRelativePath(RootDirectory, shippingPathFile),
                File.ReadAllText(shippingPathFile),
                [
                    "SharpNinja.FeatureFlags.Admin.Data.Sqlite",
                    "Admin.Data.Sqlite.csproj",
                ],
                violations);
        }
    }

    static void ValidateCentralPackageManagement(List<string> violations)
    {
        foreach (string projectFile in Directory.EnumerateFiles(RootDirectory, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsGeneratedOutput(projectFile))
            {
                continue;
            }

            XDocument project = XDocument.Load(projectFile);
            var packageReferencesWithVersions = project
                .Descendants("PackageReference")
                .Where(element => element.Attribute("Version") is not null)
                .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? "<unknown>")
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (packageReferencesWithVersions.Length > 0)
            {
                violations.Add(
                    $"{Path.GetRelativePath(RootDirectory, projectFile)} must use Directory.Packages.props for package versions: "
                    + string.Join(", ", packageReferencesWithVersions));
            }
        }
    }

    static void RequireFile(string path, List<string> violations)
    {
        if (!File.Exists(path))
        {
            violations.Add($"Missing required file {Path.GetRelativePath(RootDirectory, path)}.");
        }
    }

    static void RequireFragments(string relativePath, string source, IEnumerable<string> requiredFragments, List<string> violations)
    {
        foreach (string requiredFragment in requiredFragments)
        {
            if (!source.Contains(requiredFragment, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{relativePath} is missing required v1 configuration fragment: {requiredFragment}");
            }
        }
    }

    static void RequireAbsentFragments(string relativePath, string source, IEnumerable<string> forbiddenFragments, List<string> violations)
    {
        foreach (string forbiddenFragment in forbiddenFragments)
        {
            if (source.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"{relativePath} contains stale v1 configuration fragment: {forbiddenFragment}");
            }
        }
    }
}
