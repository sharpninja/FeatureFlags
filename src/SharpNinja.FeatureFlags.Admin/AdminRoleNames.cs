namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9: canonical Admin role names used by v1 RBAC policies.</summary>
public static class AdminRoleNames
{
    /// <summary>TR-9: role allowed to read Admin state for granted products.</summary>
    public const string Viewer = "Viewer";

    /// <summary>FR-9 TR-9: role allowed to create and edit Admin drafts for granted products.</summary>
    public const string Editor = "Editor";

    /// <summary>FR-9 FR-11 TR-9: role allowed to publish Admin drafts for granted products.</summary>
    public const string Publisher = "Publisher";

    /// <summary>FR-9 FR-11 TR-9: role allowed to promote Admin drafts between environments.</summary>
    public const string Promoter = "Promoter";

    /// <summary>TR-9: role allowed to administer product keys and bypass operation-specific role checks.</summary>
    public const string KeyAdmin = "KeyAdmin";
}
