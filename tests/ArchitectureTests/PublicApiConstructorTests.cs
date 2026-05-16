using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using SharpNinja.FeatureFlags.Abstractions;
using Xunit;

namespace ArchitectureTests;

/// <summary>TR-11 architecture tests for public injectable construction boundaries.</summary>
public sealed class PublicApiConstructorTests
{
    /// <summary>Public production types must not expose consumer-callable default constructors.</summary>
    [Fact]
    public void PublicProductionTypesDoNotExposeConsumerCallableDefaultConstructors()
    {
        var violations = LoadProductionTypes()
            .Where(IsSubjectToConstructorRule)
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsDefaultConstructable)
                .Select(ctor => $"{type.FullName}({string.Join(", ", ctor.GetParameters().Select(p => p.Name))})"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Public production types must not expose parameterless or all-default public constructors: "
            + string.Join("; ", violations));
    }

    private static readonly Regex MigrationCallPattern = new(
        @"\b(?<operation>CreateTable|DropTable|RenameTable|AddColumn|DropColumn|AlterColumn|CreateIndex|DropIndex|AddForeignKey|DropForeignKey)\s*\((?<arguments>.*?)\);",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex TableArgumentPattern = new(
        @"\btable:\s*""(?<table>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NameArgumentPattern = new(
        @"\bname:\s*""(?<table>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>TR-11 migration files must keep each schema change isolated to one table.</summary>
    [Fact]
    public void EachMigrationMutatesOnlyOneTable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationFiles = Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var violations = migrationFiles
            .Select(path => new
            {
                Path = path,
                Tables = MigrationCallPattern.Matches(File.ReadAllText(path))
                    .Select(ResolveMutatedTable)
                    .Where(table => table is not null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            })
            .Where(candidate => candidate.Tables.Length > 1)
            .Select(candidate =>
                $"{Path.GetRelativePath(repositoryRoot, candidate.Path)} mutates multiple tables: {string.Join(", ", candidate.Tables)}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Each EF migration must mutate only one table. Violations: " + string.Join("; ", violations));
    }

    private static IEnumerable<Type> LoadProductionTypes()
    {
        _ = typeof(ISharpNinjaFeatureClient).Assembly;
        var baseDirectory = AppContext.BaseDirectory;
        foreach (var path in Directory.EnumerateFiles(baseDirectory, "SharpNinja.FeatureFlags*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Contains(".Tests", StringComparison.Ordinal) || name == "SharpNinja.FeatureFlags.Build")
            {
                continue;
            }

            Assembly.LoadFrom(path);
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("SharpNinja.FeatureFlags", StringComparison.Ordinal) == true)
            .Where(assembly => assembly.GetName().Name?.Contains(".Tests", StringComparison.Ordinal) != true)
            .SelectMany(assembly => assembly.GetExportedTypes());
    }

    private static bool IsSubjectToConstructorRule(Type type)
    {
        if (!type.IsPublic)
        {
            return false;
        }

        if (type.IsInterface || type.IsEnum || type.IsAbstract || typeof(Attribute).IsAssignableFrom(type))
        {
            return false;
        }

        if (type.Namespace is null && type.Name == "Program")
        {
            return false;
        }

        if (type.Namespace?.Contains(".Abstractions.Options", StringComparison.Ordinal) == true)
        {
            return false;
        }

        // EF Core requires Migration subclasses and design-time DbContext factories to be public with default ctors.
        if (typeof(Migration).IsAssignableFrom(type))
        {
            return false;
        }

        if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>)))
        {
            return false;
        }

        // Blazor component subclasses (including Razor-compiled partials) are instantiated by the renderer
        // and require a public default constructor.
        if (typeof(ComponentBase).IsAssignableFrom(type))
        {
            return false;
        }

        // Razor _Imports.razor compiles into a public type used only as a directive carrier; the renderer
        // never instantiates it directly.
        if (string.Equals(type.Name, "_Imports", StringComparison.Ordinal))
        {
            return false;
        }

        return !IsRecord(type);
    }

    private static bool IsDefaultConstructable(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        return parameters.Length == 0 || parameters.All(parameter => parameter.IsOptional || parameter.HasDefaultValue);
    }

    private static bool IsRecord(Type type)
        => type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not null
           || type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance) is not null;

    private static string? ResolveMutatedTable(Match migrationCall)
    {
        string arguments = migrationCall.Groups["arguments"].Value;
        Match tableArgument = TableArgumentPattern.Match(arguments);
        if (tableArgument.Success)
        {
            return tableArgument.Groups["table"].Value;
        }

        Match nameArgument = NameArgumentPattern.Match(arguments);
        return nameArgument.Success ? nameArgument.Groups["table"].Value : null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "sharpninja-feature-flags.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
