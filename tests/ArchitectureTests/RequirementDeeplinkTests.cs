using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using SharpNinja.FeatureFlags.Abstractions;
using Xunit;

namespace ArchitectureTests;

/// <summary>TR-11 architecture tests: every FR/TR token in a public type's summary has a wiki deeplink in its remarks.</summary>
/// <remarks>
/// Walks every exported type from the loaded SharpNinja.FeatureFlags*.dll assemblies (excluding test and build
/// assemblies), reads each type's XML doc from the adjacent .xml file, and asserts that every FR-N and TR-N
/// token mentioned in the type's summary is backed by an <c>&lt;see href="..."/&gt;</c> element inside the
/// type's remarks. Skips out-of-v1 types, EF Core Migration subclasses, IDesignTimeDbContextFactory
/// implementations, and Blazor ComponentBase derivatives. Collects all violations into a single assertion.
/// </remarks>
public sealed class RequirementDeeplinkTests
{
    private const string FrBase =
        "https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-";

    private const string TrBase =
        "https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-";

    private static readonly Regex FrTokenPattern = new(
        @"\bFR-(\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TrTokenPattern = new(
        @"\bTR-(\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>TR-11: every FR-N and TR-N token in a public type's summary must be deep-linked from remarks.</summary>
    [Fact]
    public void PublicTypeRemarksContainDeeplinksForEveryFrTrToken()
    {
        var docCache = new Dictionary<Assembly, Dictionary<string, (string? Summary, string? Remarks)>>();

        var violations = LoadProductionTypes()
            .Where(IsSubjectToDeeplinkRule)
            .SelectMany(type => CollectViolations(type, docCache))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Every FR/TR token in a public type's summary must have a corresponding <see href> deeplink "
            + "in the type's remarks. Violations: "
            + string.Join("; ", violations));
    }

    private static IEnumerable<string> CollectViolations(
        Type type,
        Dictionary<Assembly, Dictionary<string, (string? Summary, string? Remarks)>> docCache)
    {
        var docs = GetDocsForAssembly(type.Assembly, docCache);
        var memberId = "T:" + (type.FullName ?? type.Name);
        if (!docs.TryGetValue(memberId, out var entry))
        {
            yield break;
        }

        var summary = entry.Summary ?? string.Empty;
        var remarks = entry.Remarks ?? string.Empty;

        if (summary.TrimStart().StartsWith("out-of-v1", StringComparison.Ordinal))
        {
            yield break;
        }

        var frTokens = FrTokenPattern.Matches(summary)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .Order();

        foreach (var n in frTokens)
        {
            var expected = FrBase + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!remarks.Contains(expected, StringComparison.Ordinal))
            {
                yield return $"{type.FullName} is missing FR-{n} deeplink ({expected})";
            }
        }

        var trTokens = TrTokenPattern.Matches(summary)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .Order();

        foreach (var n in trTokens)
        {
            var expected = TrBase + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!remarks.Contains(expected, StringComparison.Ordinal))
            {
                yield return $"{type.FullName} is missing TR-{n} deeplink ({expected})";
            }
        }
    }

    private static Dictionary<string, (string? Summary, string? Remarks)> GetDocsForAssembly(
        Assembly assembly,
        Dictionary<Assembly, Dictionary<string, (string? Summary, string? Remarks)>> cache)
    {
        if (cache.TryGetValue(assembly, out var existing))
        {
            return existing;
        }

        var docs = new Dictionary<string, (string? Summary, string? Remarks)>(StringComparer.Ordinal);
        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            cache[assembly] = docs;
            return docs;
        }

        var xmlPath = Path.ChangeExtension(location, ".xml");
        if (!File.Exists(xmlPath))
        {
            cache[assembly] = docs;
            return docs;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlPath);
        }
        catch (System.Xml.XmlException)
        {
            cache[assembly] = docs;
            return docs;
        }

        var members = doc.Root?.Element("members")?.Elements("member");
        if (members is null)
        {
            cache[assembly] = docs;
            return docs;
        }

        foreach (var member in members)
        {
            var name = member.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("T:", StringComparison.Ordinal))
            {
                continue;
            }

            var summary = member.Element("summary")?.ToString();
            var remarks = member.Element("remarks")?.ToString();
            docs[name] = (summary, remarks);
        }

        cache[assembly] = docs;
        return docs;
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
            .Where(assembly => assembly.GetName().Name != "SharpNinja.FeatureFlags.Build")
            .SelectMany(GetExportedTypesSafe);
    }

    private static IEnumerable<Type> GetExportedTypesSafe(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
        catch (FileNotFoundException)
        {
            // Reflection-only copied assemblies may have unsatisfied dependencies in this test context.
            return Array.Empty<Type>();
        }
        catch (TypeLoadException)
        {
            return Array.Empty<Type>();
        }
    }

    private static bool IsSubjectToDeeplinkRule(Type type)
    {
        if (!type.IsPublic && !type.IsNestedPublic)
        {
            return false;
        }

        if (typeof(Migration).IsAssignableFrom(type))
        {
            return false;
        }

        if (type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeDbContextFactory<>)))
        {
            return false;
        }

        if (typeof(ComponentBase).IsAssignableFrom(type))
        {
            return false;
        }

        // Razor _Imports.razor compiles into a public type used only as a directive carrier.
        if (string.Equals(type.Name, "_Imports", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
