using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharpNinja.FeatureFlags.Generators;

/// <summary>
/// Roslyn incremental source generator that implements partial property getters decorated
/// with <c>[FeatureFlag]</c> and emits gate guards for partial methods decorated with
/// <c>[FeatureFlagGate]</c>.
/// </summary>
/// <remarks>
/// <para>
/// For each <c>partial</c> property decorated with <c>[FeatureFlag("key")]</c> the generator
/// emits a getter that delegates to <c>_featureClient.Evaluate(key, defaultValue, _featureFlagContext).Value</c>.
/// </para>
/// <para>
/// For each <c>partial</c> method decorated with <c>[FeatureFlagGate("key")]</c> the generator
/// emits a method body that guards execution based on the flag value, with the behaviour
/// (return, throw) controlled by <c>DisabledBehavior</c>.
/// </para>
/// <para>
/// The containing class must be <c>partial</c> and must expose <c>_featureClient</c>
/// (type <c>ISharpNinjaFeatureClient</c>) and <c>_featureFlagContext</c>
/// (type <c>EvaluationContext?</c>) fields. The generator emits a compile error diagnostic
/// if the containing class is not partial.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FeatureFlagBindingGenerator : IIncrementalGenerator
{
    private const string FeatureFlagAttributeName =
        "SharpNinja.FeatureFlags.Abstractions.Attributes.FeatureFlagAttribute";

    private const string FeatureFlagGateAttributeName =
        "SharpNinja.FeatureFlags.Abstractions.Attributes.FeatureFlagGateAttribute";

    private const string FeatureFlagFallbackAttributeName =
        "SharpNinja.FeatureFlags.Abstractions.Attributes.FeatureFlagFallbackAttribute";

    private const string DisabledBehaviorTypeName =
        "SharpNinja.FeatureFlags.Abstractions.Attributes.DisabledBehavior";

    // Diagnostic for non-partial containing class.
    private static readonly DiagnosticDescriptor NonPartialClassDiagnostic = new(
        id: "SNFF001",
        title: "Containing class must be partial",
        messageFormat: "Class '{0}' must be declared as partial to use FeatureFlag generators",
        category: "SharpNinja.FeatureFlags.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all property declarations with [FeatureFlag] attribute.
        IncrementalValuesProvider<PropertyDeclarationSyntax> flaggedProperties =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsPropertyWithAttribute(node),
                    transform: static (ctx, _) => (PropertyDeclarationSyntax)ctx.Node)
                .Where(static p => p is not null)!;

        // Collect all method declarations with [FeatureFlagGate] attribute.
        IncrementalValuesProvider<MethodDeclarationSyntax> gatedMethods =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsMethodWithAttribute(node),
                    transform: static (ctx, _) => (MethodDeclarationSyntax)ctx.Node)
                .Where(static m => m is not null)!;

        // Combine with compilation to resolve symbols.
        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<PropertyDeclarationSyntax> Properties)>
            propertiesWithCompilation = context.CompilationProvider.Combine(flaggedProperties.Collect());

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<MethodDeclarationSyntax> Methods)>
            methodsWithCompilation = context.CompilationProvider.Combine(gatedMethods.Collect());

        context.RegisterSourceOutput(propertiesWithCompilation, static (spc, source) =>
            GeneratePropertyImplementations(spc, source.Compilation, source.Properties));

        context.RegisterSourceOutput(methodsWithCompilation, static (spc, source) =>
            GenerateMethodImplementations(spc, source.Compilation, source.Methods));
    }

    private static bool IsPropertyWithAttribute(SyntaxNode node)
    {
        if (node is not PropertyDeclarationSyntax property)
        {
            return false;
        }

        return property.AttributeLists.Count > 0
            && property.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static bool IsMethodWithAttribute(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method)
        {
            return false;
        }

        return method.AttributeLists.Count > 0
            && method.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static void GeneratePropertyImplementations(
        SourceProductionContext spc,
        Compilation compilation,
        ImmutableArray<PropertyDeclarationSyntax> properties)
    {
        INamedTypeSymbol? flagAttrSymbol = compilation.GetTypeByMetadataName(FeatureFlagAttributeName);
        INamedTypeSymbol? fallbackAttrSymbol = compilation.GetTypeByMetadataName(FeatureFlagFallbackAttributeName);

        if (flagAttrSymbol is null)
        {
            return;
        }

        // Group by containing class to emit one file per class.
        var grouped = new Dictionary<string, (INamedTypeSymbol ClassSymbol, List<(IPropertySymbol Property, string Key, string? FallbackMember)> Members)>(StringComparer.Ordinal);

        foreach (PropertyDeclarationSyntax propSyntax in properties)
        {
            SemanticModel model = compilation.GetSemanticModel(propSyntax.SyntaxTree);
            if (model.GetDeclaredSymbol(propSyntax) is not IPropertySymbol propSymbol)
            {
                continue;
            }

            // Find [FeatureFlag] attribute on this property.
            string? flagKey = GetAttributeStringArg(propSymbol, flagAttrSymbol);
            if (flagKey is null)
            {
                continue;
            }

            // Containing class must be partial.
            INamedTypeSymbol? classSymbol = propSymbol.ContainingType;
            if (classSymbol is null)
            {
                continue;
            }

            if (!IsPartialClass(classSymbol, propSyntax))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    NonPartialClassDiagnostic,
                    propSyntax.GetLocation(),
                    classSymbol.Name));
                continue;
            }

            // Optional fallback member name.
            string? fallbackMember = fallbackAttrSymbol is not null
                ? GetAttributeStringArg(propSymbol, fallbackAttrSymbol)
                : null;

            string classKey = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!grouped.TryGetValue(classKey, out var entry))
            {
                entry = (classSymbol, new List<(IPropertySymbol, string, string?)>());
                grouped[classKey] = entry;
            }

            entry.Members.Add((propSymbol, flagKey, fallbackMember));
        }

        foreach (var kvp in grouped)
        {
            var (classSymbol, members) = kvp.Value;
            string source = GeneratePropertyFile(classSymbol, members);
            string hint = $"{classSymbol.Name}_FeatureFlags_Properties.g.cs";
            spc.AddSource(hint, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void GenerateMethodImplementations(
        SourceProductionContext spc,
        Compilation compilation,
        ImmutableArray<MethodDeclarationSyntax> methods)
    {
        INamedTypeSymbol? gateAttrSymbol = compilation.GetTypeByMetadataName(FeatureFlagGateAttributeName);
        INamedTypeSymbol? disabledBehaviorSymbol = compilation.GetTypeByMetadataName(DisabledBehaviorTypeName);

        if (gateAttrSymbol is null)
        {
            return;
        }

        var grouped = new Dictionary<string, (INamedTypeSymbol ClassSymbol, List<(IMethodSymbol Method, string Key, int DisabledBehavior)> Members)>(StringComparer.Ordinal);

        foreach (MethodDeclarationSyntax methodSyntax in methods)
        {
            SemanticModel model = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            if (model.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            // Find [FeatureFlagGate] attribute on this method.
            (string? flagKey, int disabledBehavior) = GetGateAttributeArgs(methodSymbol, gateAttrSymbol);
            if (flagKey is null)
            {
                continue;
            }

            INamedTypeSymbol? classSymbol = methodSymbol.ContainingType;
            if (classSymbol is null)
            {
                continue;
            }

            if (!IsPartialClass(classSymbol, methodSyntax))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    NonPartialClassDiagnostic,
                    methodSyntax.GetLocation(),
                    classSymbol.Name));
                continue;
            }

            string classKey = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!grouped.TryGetValue(classKey, out var entry))
            {
                entry = (classSymbol, new List<(IMethodSymbol, string, int)>());
                grouped[classKey] = entry;
            }

            entry.Members.Add((methodSymbol, flagKey, disabledBehavior));
        }

        foreach (var kvp in grouped)
        {
            var (classSymbol, members) = kvp.Value;
            string source = GenerateMethodFile(classSymbol, members);
            string hint = $"{classSymbol.Name}_FeatureFlags_Gates.g.cs";
            spc.AddSource(hint, SourceText.From(source, Encoding.UTF8));
        }
    }

    // ---- Code emission helpers ----

    private static string GeneratePropertyFile(
        INamedTypeSymbol classSymbol,
        List<(IPropertySymbol Property, string Key, string? FallbackMember)> members)
    {
        string ns = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        string className = classSymbol.Name;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by SharpNinja.FeatureFlags.Generators.FeatureFlagBindingGenerator");
        sb.AppendLine("// Do not edit this file manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {className}");
        sb.AppendLine("{");

        foreach (var (property, key, fallbackMember) in members)
        {
            string escapedKey = EscapeStringLiteral(key);
            string returnType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string defaultValue = GetDefaultValueExpression(property.Type, fallbackMember);

            sb.AppendLine($"    /// <summary>Generated feature flag accessor for key <c>{key}</c>.</summary>");
            sb.AppendLine($"    public partial {returnType} {property.Name} =>");
            sb.AppendLine($"        _featureClient.Evaluate(\"{escapedKey}\", {defaultValue}, _featureFlagContext).Value;");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateMethodFile(
        INamedTypeSymbol classSymbol,
        List<(IMethodSymbol Method, string Key, int DisabledBehavior)> members)
    {
        string ns = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        string className = classSymbol.Name;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by SharpNinja.FeatureFlags.Generators.FeatureFlagBindingGenerator");
        sb.AppendLine("// Do not edit this file manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial class {className}");
        sb.AppendLine("{");

        foreach (var (method, key, disabledBehavior) in members)
        {
            string escapedKey = EscapeStringLiteral(key);
            string methodSignature = BuildMethodSignature(method);

            sb.AppendLine($"    /// <summary>Generated feature flag gate for key <c>{key}</c>.</summary>");
            sb.AppendLine($"    {methodSignature}");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (!_featureClient.Evaluate(\"{escapedKey}\", false, _featureFlagContext).Value)");
            sb.AppendLine("        {");

            // DisabledBehavior: 0 = ReturnFallback, 1 = Skip, 2 = Throw
            if (disabledBehavior == 2)
            {
                sb.AppendLine($"            throw new global::SharpNinja.FeatureFlags.Abstractions.FeatureFlagDisabledException(\"{escapedKey}\");");
            }
            else
            {
                // ReturnFallback (0) and Skip (1) both return without executing the body.
                sb.AppendLine("            return;");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ---- Symbol helpers ----

    private static string? GetAttributeStringArg(ISymbol symbol, INamedTypeSymbol attrTypeSymbol)
    {
        foreach (AttributeData attr in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrTypeSymbol))
            {
                continue;
            }

            if (attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static (string? Key, int DisabledBehavior) GetGateAttributeArgs(
        IMethodSymbol method,
        INamedTypeSymbol attrTypeSymbol)
    {
        foreach (AttributeData attr in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrTypeSymbol))
            {
                continue;
            }

            string? key = null;
            if (attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Value is string k)
            {
                key = k;
            }

            // DisabledBehavior is a named argument with default 0 (ReturnFallback).
            int disabledBehavior = 0;
            foreach (KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
            {
                if (string.Equals(namedArg.Key, "DisabledBehavior", StringComparison.Ordinal)
                    && namedArg.Value.Value is int behaviorValue)
                {
                    disabledBehavior = behaviorValue;
                }
            }

            return (key, disabledBehavior);
        }

        return (null, 0);
    }

    private static bool IsPartialClass(INamedTypeSymbol classSymbol, SyntaxNode memberSyntax)
    {
        // Walk up the syntax tree to find the class declaration and check for 'partial' modifier.
        SyntaxNode? parent = memberSyntax.Parent;
        while (parent is not null)
        {
            if (parent is ClassDeclarationSyntax classDecl)
            {
                return classDecl.Modifiers.Any(SyntaxKind.PartialKeyword);
            }

            parent = parent.Parent;
        }

        // Fallback: check declared syntax references for any partial modifier.
        foreach (SyntaxReference sr in classSymbol.DeclaringSyntaxReferences)
        {
            if (sr.GetSyntax() is ClassDeclarationSyntax classDecl
                && classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDefaultValueExpression(ITypeSymbol typeSymbol, string? fallbackMember)
    {
        if (fallbackMember is not null)
        {
            return fallbackMember;
        }

        // Produce a compile-time default for well-known types.
        return typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean => "false",
            SpecialType.System_String => "string.Empty",
            SpecialType.System_Int32 => "0",
            SpecialType.System_Int64 => "0L",
            SpecialType.System_Double => "0.0",
            SpecialType.System_Single => "0.0f",
            SpecialType.System_Decimal => "0m",
            _ => $"default({typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})",
        };
    }

    private static string BuildMethodSignature(IMethodSymbol method)
    {
        string accessibility = method.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "public",
        };

        string returnType = method.ReturnsVoid
            ? "void"
            : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Build parameter list.
        var paramParts = new List<string>();
        foreach (IParameterSymbol param in method.Parameters)
        {
            string refKind = param.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty,
            };

            paramParts.Add($"{refKind}{param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {param.Name}");
        }

        string parameters = string.Join(", ", paramParts);

        return $"partial {returnType} {method.Name}({parameters})";
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
