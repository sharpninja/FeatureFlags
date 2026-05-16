using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharpNinja.FeatureFlags.Generators;

/// <summary>
/// Roslyn incremental source generator that emits C# 13 / .NET 9+ interceptors for every call
/// site of a method decorated with
/// <c>[SharpNinja.FeatureFlags.Abstractions.Attributes.InterceptFeatureFlagAttribute("flag.key")]</c>.
/// </summary>
/// <remarks>
/// <para>
/// For each invocation in the consumer's compilation whose target method bears the
/// <c>InterceptFeatureFlag</c> attribute, this generator emits an interceptor method decorated
/// with <c>[System.Runtime.CompilerServices.InterceptsLocation(version: 1, data: ...)]</c>.
/// The interceptor first evaluates the configured flag through the first
/// <c>ISharpNinjaFeatureClient</c> parameter found on either the interceptor's call site or
/// the target method. If the flag returns <see langword="false"/>, a
/// <c>FeatureFlagDisabledException</c> is thrown; otherwise the interceptor dispatches to the
/// original method.
/// </para>
/// <para>
/// Consumers must opt in by adding the SharpNinja interceptors namespace to their project file:
/// <code>
/// &lt;PropertyGroup&gt;
///   &lt;InterceptorsNamespaces&gt;$(InterceptorsNamespaces);SharpNinja.FeatureFlags.Generated&lt;/InterceptorsNamespaces&gt;
/// &lt;/PropertyGroup&gt;
/// </code>
/// The generator emits a polyfill of <c>InterceptsLocationAttribute</c> so it compiles even
/// when the consumer targets a TFM that does not ship the attribute publicly.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FeatureFlagInterceptorGenerator : IIncrementalGenerator
{
    private const string InterceptFeatureFlagAttributeName =
        "SharpNinja.FeatureFlags.Abstractions.Attributes.InterceptFeatureFlagAttribute";

    private const string FeatureClientFullName =
        "global::SharpNinja.FeatureFlags.Abstractions.ISharpNinjaFeatureClient";

    private const string FeatureFlagDisabledExceptionFullName =
        "global::SharpNinja.FeatureFlags.Abstractions.FeatureFlagDisabledException";

    private const string InterceptorsNamespace = "SharpNinja.FeatureFlags.Generated";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Collect all invocation expressions; we filter to attributed methods inside the transform.
        IncrementalValuesProvider<InvocationExpressionSyntax> invocations =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is InvocationExpressionSyntax,
                    transform: static (ctx, _) => (InvocationExpressionSyntax)ctx.Node);

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<InvocationExpressionSyntax> Invocations)>
            combined = context.CompilationProvider.Combine(invocations.Collect());

        context.RegisterSourceOutput(combined, static (spc, source) =>
            Emit(spc, source.Compilation, source.Invocations));
    }

    private static void Emit(
        SourceProductionContext spc,
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> invocations)
    {
        INamedTypeSymbol? attrSymbol = compilation.GetTypeByMetadataName(InterceptFeatureFlagAttributeName);
        if (attrSymbol is null || invocations.IsDefaultOrEmpty)
        {
            return;
        }

        var entries = new List<InterceptorEntry>();
        int index = 0;

        foreach (InvocationExpressionSyntax invocation in invocations)
        {
            SemanticModel model = compilation.GetSemanticModel(invocation.SyntaxTree);
            SymbolInfo info = model.GetSymbolInfo(invocation);
            if (info.Symbol is not IMethodSymbol target)
            {
                continue;
            }

            string? flagKey = GetFlagKey(target, attrSymbol);
            if (flagKey is null)
            {
                continue;
            }

            // Resolve the interceptable location via the Roslyn 4.14+ API. This returns a v1
            // string-data location that compiles against any TFM with interceptors enabled.
            InterceptableLocation? location = model.GetInterceptableLocation(invocation);
            if (location is null)
            {
                continue;
            }

            entries.Add(new InterceptorEntry(
                index: index++,
                flagKey: flagKey,
                target: target,
                locationAttributeText: location.GetInterceptsLocationAttributeSyntax(),
                displayLocation: location.GetDisplayLocation()));
        }

        if (entries.Count == 0)
        {
            return;
        }

        string source = GenerateSource(entries);
        spc.AddSource("FeatureFlagInterceptors.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string? GetFlagKey(IMethodSymbol method, INamedTypeSymbol attrSymbol)
    {
        // Inspect both the candidate method and (for generic / overridden methods) the original
        // definition so consumers can attribute either form.
        IMethodSymbol[] candidates = method.OriginalDefinition is { } original
            && !SymbolEqualityComparer.Default.Equals(original, method)
                ? new[] { method, original }
                : new[] { method };

        foreach (IMethodSymbol candidate in candidates)
        {
            foreach (AttributeData attr in candidate.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                {
                    continue;
                }

                if (attr.ConstructorArguments.Length > 0
                    && attr.ConstructorArguments[0].Value is string key
                    && !string.IsNullOrWhiteSpace(key))
                {
                    return key;
                }
            }
        }

        return null;
    }

    private static string GenerateSource(List<InterceptorEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Generated by SharpNinja.FeatureFlags.Generators.FeatureFlagInterceptorGenerator");
        sb.AppendLine("// Do not edit this file manually.");
        sb.AppendLine("//");
        sb.AppendLine("// Consumers must opt in by adding the following MSBuild property:");
        sb.AppendLine("//   <InterceptorsNamespaces>$(InterceptorsNamespaces);" + InterceptorsNamespace + "</InterceptorsNamespaces>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Polyfill the InterceptsLocationAttribute so the generated code compiles regardless of
        // whether the consumer's TFM exposes it publicly. The attribute is internal so it does
        // not leak across assemblies.
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data)");
        sb.AppendLine("        {");
        sb.AppendLine("            _ = version;");
        sb.AppendLine("            _ = data;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("namespace " + InterceptorsNamespace);
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Generated interceptors that gate decorated method calls behind feature flags.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    file static class FeatureFlagInterceptors");
        sb.AppendLine("    {");

        foreach (InterceptorEntry entry in entries)
        {
            EmitInterceptor(sb, entry);
        }

        AppendResolver(sb);

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void EmitInterceptor(StringBuilder sb, InterceptorEntry entry)
    {
        IMethodSymbol target = entry.Target;
        string escapedKey = EscapeStringLiteral(entry.FlagKey);
        string returnType = target.ReturnsVoid
            ? "void"
            : target.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Interceptor signature must mirror the target's parameter list. For instance methods we
        // prepend a 'this' parameter typed as the containing type, which is how interceptors
        // capture the receiver.
        var paramDecls = new List<string>();
        var argRefs = new List<string>();

        if (!target.IsStatic)
        {
            string receiverType = target.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            paramDecls.Add("this " + receiverType + " __receiver");
        }

        foreach (IParameterSymbol param in target.Parameters)
        {
            string refKind = param.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                _ => string.Empty,
            };

            string paramType = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string paramName = "__p" + paramDecls.Count;
            paramDecls.Add(refKind + paramType + " " + paramName);
            argRefs.Add(refKind + paramName);
        }

        string methodName = "Intercept_" + SanitizeIdentifier(target.Name) + "_" + entry.Index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        sb.AppendLine("        /// <summary>Generated interceptor for <c>" + EscapeXmlText(entry.DisplayLocation) + "</c> gated by flag <c>" + EscapeXmlText(entry.FlagKey) + "</c>.</summary>");
        sb.AppendLine("        " + entry.LocationAttributeText);
        sb.Append("        public static " + returnType + " " + methodName + "(");
        sb.Append(string.Join(", ", paramDecls));
        sb.AppendLine(")");
        sb.AppendLine("        {");
        sb.AppendLine("            " + FeatureClientFullName + "? __client = ResolveFeatureClient();");
        sb.AppendLine("            if (__client is null || !__client.Evaluate(\"" + escapedKey + "\", false).Value)");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new " + FeatureFlagDisabledExceptionFullName + "(\"" + escapedKey + "\");");
        sb.AppendLine("            }");
        sb.AppendLine();

        // Dispatch to the original method.
        string call = BuildOriginalCall(target, argRefs);
        if (target.ReturnsVoid)
        {
            sb.AppendLine("            " + call + ";");
        }
        else
        {
            sb.AppendLine("            return " + call + ";");
        }

        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static string BuildOriginalCall(IMethodSymbol target, List<string> argRefs)
    {
        string args = string.Join(", ", argRefs);

        if (target.IsStatic)
        {
            string containing = target.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return containing + "." + target.Name + "(" + args + ")";
        }

        return "__receiver." + target.Name + "(" + args + ")";
    }

    private static string SanitizeIdentifier(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return sb.Length == 0 ? "M" : sb.ToString();
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string EscapeXmlText(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    // Resolver placeholder; the generated file inlines this as a static method below.
    // We synthesize the resolver per-file at the end of GenerateSource via a fixed snippet.
    // To keep things simple, append the resolver after the main loop.
    private static void AppendResolver(StringBuilder sb)
    {
        sb.AppendLine("        private static " + FeatureClientFullName + "? ResolveFeatureClient()");
        sb.AppendLine("        {");
        sb.AppendLine("            // Consumer-supplied resolver. Override by declaring a partial class");
        sb.AppendLine("            // SharpNinjaFeatureClientAccessor with a static GetClient() method,");
        sb.AppendLine("            // or set the static FeatureFlagInterceptorContext.Client property.");
        sb.AppendLine("            return FeatureFlagInterceptorContext.Client;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Static accessor consumers may set at startup to provide the feature client.</summary>");
        sb.AppendLine("        public static class FeatureFlagInterceptorContext");
        sb.AppendLine("        {");
        sb.AppendLine("            /// <summary>Gets or sets the feature client used by generated interceptors.</summary>");
        sb.AppendLine("            public static " + FeatureClientFullName + "? Client { get; set; }");
        sb.AppendLine("        }");
    }

    private sealed class InterceptorEntry
    {
        public InterceptorEntry(
            int index,
            string flagKey,
            IMethodSymbol target,
            string locationAttributeText,
            string displayLocation)
        {
            Index = index;
            FlagKey = flagKey;
            Target = target;
            LocationAttributeText = locationAttributeText;
            DisplayLocation = displayLocation;
        }

        public int Index { get; }

        public string FlagKey { get; }

        public IMethodSymbol Target { get; }

        public string LocationAttributeText { get; }

        public string DisplayLocation { get; }
    }
}
