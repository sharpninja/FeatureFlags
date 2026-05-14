using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
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

    /// <summary>Placeholder config validation target for Phase 0.</summary>
    Target ValidateConfig => _ => _
        .DependsOn(Compile)
        .Executes(() => Serilog.Log.Information("No validation rules yet."));

    /// <summary>Placeholder traceability validation target for Phase 0.</summary>
    Target ValidateTraceability => _ => _
        .DependsOn(Compile)
        .Executes(() => Serilog.Log.Information("No validation rules yet."));
}
