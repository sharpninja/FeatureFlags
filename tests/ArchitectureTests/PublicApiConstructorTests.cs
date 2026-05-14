using System.Reflection;
using Byrd.FeatureFlags.Abstractions;
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

    /// <summary>Phase 4 migration discipline test scaffold.</summary>
    [Fact(Skip = "Phase 4 enables this once migration assemblies contain EF Core migrations.")]
    public void EachMigrationMutatesOnlyOneTable()
    {
    }

    private static IEnumerable<Type> LoadProductionTypes()
    {
        _ = typeof(IByrdFeatureClient).Assembly;
        var baseDirectory = AppContext.BaseDirectory;
        foreach (var path in Directory.EnumerateFiles(baseDirectory, "Byrd.FeatureFlags*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Contains(".Tests", StringComparison.Ordinal) || name == "Byrd.FeatureFlags.Build")
            {
                continue;
            }

            Assembly.LoadFrom(path);
        }

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Byrd.FeatureFlags", StringComparison.Ordinal) == true)
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
}
