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
