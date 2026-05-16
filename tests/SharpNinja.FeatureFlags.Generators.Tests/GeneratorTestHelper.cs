using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharpNinja.FeatureFlags.Generators;

namespace SharpNinja.FeatureFlags.Generators.Tests;

/// <summary>
/// Helper utilities for running Roslyn source generators in unit tests.
/// Provides factory methods for creating compilations and running generators.
/// </summary>
internal static class GeneratorTestHelper
{
    /// <summary>
    /// Creates a <see cref="CSharpCompilation"/> containing the supplied source text
    /// plus references to the Abstractions assembly.
    /// </summary>
    /// <param name="source">The C# source code to compile.</param>
    /// <param name="assemblyName">Optional assembly name; defaults to <c>TestAssembly</c>.</param>
    /// <returns>A compilation ready for generator testing.</returns>
    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var references = BuildReferences();

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Runs a single incremental generator against a compilation and returns
    /// the driver result containing all generated sources.
    /// </summary>
    /// <typeparam name="TGenerator">The generator type to run.</typeparam>
    /// <param name="compilation">The input compilation.</param>
    /// <returns>The generator driver result.</returns>
    public static GeneratorDriverRunResult RunGenerator<TGenerator>(CSharpCompilation compilation)
        where TGenerator : IIncrementalGenerator, new()
    {
        var generator = new TGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(generator)
            .RunGenerators(compilation);
        return driver.GetRunResult();
    }

    /// <summary>
    /// Returns the generated source text for a given hint name, or <see langword="null"/>
    /// if no source with that hint was produced.
    /// </summary>
    /// <param name="result">The driver run result.</param>
    /// <param name="hintName">The hint name (file name) to search for.</param>
    /// <returns>The generated source text, or <see langword="null"/>.</returns>
    public static string? GetGeneratedSource(GeneratorDriverRunResult result, string hintName)
    {
        foreach (GeneratorRunResult generatorResult in result.Results)
        {
            foreach (GeneratedSourceResult source in generatorResult.GeneratedSources)
            {
                if (source.HintName == hintName)
                {
                    return source.SourceText.ToString();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all generated source texts from the driver run result.
    /// </summary>
    /// <param name="result">The driver run result.</param>
    /// <returns>A list of hint name and source text pairs.</returns>
    public static IReadOnlyList<(string HintName, string Source)> GetAllGeneratedSources(
        GeneratorDriverRunResult result)
    {
        var sources = new List<(string, string)>();
        foreach (GeneratorRunResult generatorResult in result.Results)
        {
            foreach (GeneratedSourceResult source in generatorResult.GeneratedSources)
            {
                sources.Add((source.HintName, source.SourceText.ToString()));
            }
        }

        return sources;
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>();

        // Core runtime references.
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (trustedAssemblies is not null)
        {
            foreach (string path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    refs.Add(MetadataReference.CreateFromFile(path));
                }
            }
        }
        else
        {
            // Fallback for environments without TRUSTED_PLATFORM_ASSEMBLIES.
            refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location));
        }

        // Abstractions assembly (contains the attribute types the generators look for).
        string abstractionsPath = typeof(SharpNinja.FeatureFlags.Abstractions.Attributes.ProductScopeAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(abstractionsPath) && File.Exists(abstractionsPath))
        {
            refs.Add(MetadataReference.CreateFromFile(abstractionsPath));
        }

        return [.. refs];
    }
}
