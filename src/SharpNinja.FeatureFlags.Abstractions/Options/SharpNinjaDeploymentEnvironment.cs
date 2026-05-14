namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-11 v1 contract: deployment environment supporting built-in and custom environment names.</summary>
/// <param name="Name">Environment name used for manifest selection and promotion workflows.</param>
public sealed record SharpNinjaDeploymentEnvironment(string Name)
{
    /// <summary>FR-11 v1 contract: development environment.</summary>
    public static SharpNinjaDeploymentEnvironment Development { get; } = new("development");

    /// <summary>FR-11 v1 contract: staging environment.</summary>
    public static SharpNinjaDeploymentEnvironment Staging { get; } = new("staging");

    /// <summary>FR-11 v1 contract: production environment.</summary>
    public static SharpNinjaDeploymentEnvironment Production { get; } = new("production");

    /// <summary>FR-11 v1 contract: creates a deployment environment from a configured name.</summary>
    /// <param name="name">Environment name.</param>
    /// <returns>A deployment environment.</returns>
    public static SharpNinjaDeploymentEnvironment Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(name);
    }

    /// <summary>FR-11 v1 contract: determines whether the environment is one of the built-in names.</summary>
    /// <returns><see langword="true" /> when the name is development, staging, or production.</returns>
    public bool IsBuiltIn()
        => string.Equals(Name, Development.Name, StringComparison.Ordinal)
            || string.Equals(Name, Staging.Name, StringComparison.Ordinal)
            || string.Equals(Name, Production.Name, StringComparison.Ordinal);

    /// <summary>FR-11 v1 contract: validates environment naming and custom-environment policy.</summary>
    /// <param name="allowCustomEnvironments">Whether non-built-in environment names are allowed.</param>
    /// <returns>The current deployment environment when validation succeeds.</returns>
    /// <exception cref="ArgumentException">Thrown when the environment name is blank.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the name is custom and custom environments are disabled.</exception>
    public SharpNinjaDeploymentEnvironment Validate(bool allowCustomEnvironments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);

        if (!allowCustomEnvironments && !IsBuiltIn())
        {
            throw new InvalidOperationException(
                $"Environment '{Name}' is custom, but custom environments are disabled.");
        }

        return this;
    }
}
