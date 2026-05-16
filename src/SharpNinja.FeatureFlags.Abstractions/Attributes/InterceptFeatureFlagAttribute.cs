namespace SharpNinja.FeatureFlags.Abstractions.Attributes;

/// <summary>
/// FR-7 FR-12: marks a method so the SharpNinja Roslyn interceptor generator can intercept
/// every call site of the method and gate execution behind a feature flag.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="FeatureFlagGateAttribute"/> (which requires a <c>partial</c> method and
/// is resolved at the declaration), this attribute uses C# 13 / .NET 9+ interceptors. The
/// SharpNinja generator scans call sites of methods tagged with this attribute and emits
/// an interceptor for each call site that checks the flag before dispatching to the original
/// method. If the flag evaluates to <see langword="false"/>, a
/// <see cref="SharpNinja.FeatureFlags.Abstractions.FeatureFlagDisabledException"/> is thrown.
/// </para>
/// <para>
/// Interceptors require the consuming project to opt in via the
/// <c>&lt;InterceptorsNamespaces&gt;</c> MSBuild property (or the legacy
/// <c>&lt;InterceptorsPreviewNamespaces&gt;</c>). The SharpNinja generator emits interceptors
/// under the <c>SharpNinja.FeatureFlags.Generated</c> namespace.
/// </para>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-7"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-12"/>
/// </remarks>
/// <param name="flagKey">
/// The feature flag key gating call sites of the decorated method. Must be a non-empty,
/// non-whitespace string.
/// </param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class InterceptFeatureFlagAttribute(string flagKey) : Attribute
{
    /// <summary>Gets the feature flag key gating call sites of the decorated method.</summary>
    public string FlagKey { get; } = ValidateFlagKey(flagKey);

    private static string ValidateFlagKey(string flagKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        return flagKey.Trim();
    }
}
