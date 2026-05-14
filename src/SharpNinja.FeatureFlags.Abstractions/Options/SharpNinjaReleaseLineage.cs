namespace SharpNinja.FeatureFlags.Abstractions.Options;

/// <summary>FR-1 v1 contract: immutable release lineage for semver plus channel and build.</summary>
/// <param name="ReleaseId">Immutable release identifier stamped into the build.</param>
/// <param name="SemanticVersion">Strict semantic version for the product release.</param>
/// <param name="Channel">Release channel for staged rollout targeting.</param>
/// <param name="Build">Build identifier within the semantic version and channel.</param>
public sealed record SharpNinjaReleaseLineage(
    string ReleaseId,
    string SemanticVersion,
    SharpNinjaReleaseChannel Channel,
    string Build)
{
    /// <summary>FR-1 v1 contract: default local stable release lineage.</summary>
    public static SharpNinjaReleaseLineage Default { get; } =
        new("truckmate-0.0.0-stable-0", "0.0.0", SharpNinjaReleaseChannel.Stable, "0");

    /// <summary>FR-1 v1 contract: creates compatibility lineage when only a release id is available.</summary>
    /// <param name="releaseId">Immutable release identifier stamped into the build.</param>
    /// <returns>Release lineage using a valid default semantic version, stable channel, and build zero.</returns>
    public static SharpNinjaReleaseLineage FromReleaseId(string releaseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseId);
        return new(releaseId, "0.0.0", SharpNinjaReleaseChannel.Stable, "0");
    }

    /// <summary>FR-1 v1 contract: validates release lineage invariants.</summary>
    /// <returns>The current release lineage when validation succeeds.</returns>
    /// <exception cref="ArgumentException">Thrown when a required string value is blank.</exception>
    /// <exception cref="InvalidOperationException">Thrown when semantic version or channel values are invalid.</exception>
    public SharpNinjaReleaseLineage Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ReleaseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SemanticVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(Build);

        if (!Enum.IsDefined(Channel))
        {
            throw new InvalidOperationException($"Release channel '{Channel}' is not supported.");
        }

        if (!IsSemanticVersion(SemanticVersion))
        {
            throw new InvalidOperationException(
                $"{nameof(SemanticVersion)} must be a strict semantic version in major.minor.patch form.");
        }

        return this;
    }

    private static bool IsSemanticVersion(string value)
    {
        string[] buildParts = value.Split('+', 2, StringSplitOptions.None);
        if (buildParts.Length == 2 && !HasValidIdentifiers(buildParts[1]))
        {
            return false;
        }

        string[] prereleaseParts = buildParts[0].Split('-', 2, StringSplitOptions.None);
        if (prereleaseParts.Length == 2 && !HasValidIdentifiers(prereleaseParts[1]))
        {
            return false;
        }

        string[] coreParts = prereleaseParts[0].Split('.', StringSplitOptions.None);
        if (coreParts.Length != 3)
        {
            return false;
        }

        foreach (string corePart in coreParts)
        {
            if (!IsNonNegativeIntegerIdentifier(corePart))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidIdentifiers(string value)
    {
        string[] identifiers = value.Split('.', StringSplitOptions.None);
        if (identifiers.Length == 0)
        {
            return false;
        }

        foreach (string identifier in identifiers)
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            foreach (char character in identifier)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsNonNegativeIntegerIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (value.Length > 1 && value[0] == '0')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
