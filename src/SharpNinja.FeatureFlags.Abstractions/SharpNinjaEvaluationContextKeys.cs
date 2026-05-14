namespace SharpNinja.FeatureFlags.Abstractions;

/// <summary>FR-1 FR-8 FR-11 TR-9 v1 contract: well-known evaluation context keys.</summary>
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
