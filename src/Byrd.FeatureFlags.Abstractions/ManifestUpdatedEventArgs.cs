namespace Byrd.FeatureFlags.Abstractions;

/// <summary>FR-3 Phase 0 contract: event data for manifest update notifications.</summary>
public sealed class ManifestUpdatedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ManifestUpdatedEventArgs"/> class.</summary>
    /// <param name="manifestId">Updated manifest identifier.</param>
    /// <param name="updatedAt">Update timestamp.</param>
    public ManifestUpdatedEventArgs(string manifestId, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestId);
        ManifestId = manifestId;
        UpdatedAt = updatedAt;
    }

    /// <summary>Gets the updated manifest identifier.</summary>
    public string ManifestId { get; }

    /// <summary>Gets the update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; }
}
