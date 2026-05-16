namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-1 FR-8 FR-11 TR-9 v1 contract: well-known evaluation context keys.</summary>
/// <remarks>
/// Static constants. Values are part of the public wire contract; do not rename.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-1"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-8"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-9"/>
/// </remarks>
public static class SharpNinjaEvaluationContextKeys
{
    /// <summary>FR-1 v1 contract: context key for compile-time product identity.</summary>
    public const string ProductId = "ProductId";

    /// <summary>FR-1 v1 contract: context key for compile-time release identity.</summary>
    public const string ReleaseId = "ReleaseId";

    /// <summary>FR-1 v1 contract: context key for semantic release version.</summary>
    public const string SemanticVersion = "SemanticVersion";

    /// <summary>FR-1 v1 contract: context key for release channel.</summary>
    public const string ReleaseChannel = "ReleaseChannel";

    /// <summary>FR-1 v1 contract: context key for release build identifier.</summary>
    public const string ReleaseBuild = "ReleaseBuild";

    /// <summary>FR-11 v1 contract: context key for deployment environment.</summary>
    public const string Environment = "Environment";

    /// <summary>TR-9 v1 contract: context key for tenant identity.</summary>
    public const string TenantId = "TenantId";
}
