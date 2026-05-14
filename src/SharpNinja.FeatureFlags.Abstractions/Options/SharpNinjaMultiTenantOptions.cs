namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>TR-9 v1 contract: multi-tenant deployment options for tenant-aware manifest and exposure flows.</summary>
/// <param name="Enabled">Whether tenant isolation is enabled.</param>
/// <param name="TenantContextKey">Evaluation context key used to carry tenant identity.</param>
/// <param name="DefaultTenantId">Optional default tenant identifier for single-tenant hosts or local development.</param>
public sealed record SharpNinjaMultiTenantOptions(
    bool Enabled,
    string TenantContextKey,
    string? DefaultTenantId = null)
{
    /// <summary>TR-9 v1 contract: default tenant context key.</summary>
    public const string DefaultTenantContextKey = "TenantId";

    /// <summary>TR-9 v1 contract: default single-tenant deployment options.</summary>
    public static SharpNinjaMultiTenantOptions SingleTenant { get; } =
        new(false, DefaultTenantContextKey);

    /// <summary>TR-9 v1 contract: default multi-tenant deployment options.</summary>
    public static SharpNinjaMultiTenantOptions MultiTenant { get; } =
        new(true, DefaultTenantContextKey);

    /// <summary>TR-9 v1 contract: validates tenant option invariants.</summary>
    /// <returns>The current tenant options when validation succeeds.</returns>
    /// <exception cref="ArgumentException">Thrown when a tenant string option is invalid.</exception>
    public SharpNinjaMultiTenantOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(TenantContextKey);

        if (DefaultTenantId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(DefaultTenantId);
        }

        return this;
    }
}
