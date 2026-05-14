namespace SharpNinja.FeatureFlags.Admin;

/// <summary>FR-9 FR-10 FR-11 TR-9: named Admin authorization policies for v1 operations.</summary>
public static class AdminPolicyNames
{
    /// <summary>TR-9: policy name for read-only Admin access.</summary>
    public const string Read = "SharpNinjaAdminRead";

    /// <summary>FR-9 TR-9: policy name for draft edit access.</summary>
    public const string Edit = "SharpNinjaAdminEdit";

    /// <summary>FR-9 FR-11 TR-9: policy name for publish access.</summary>
    public const string Publish = "SharpNinjaAdminPublish";

    /// <summary>FR-9 FR-11 TR-9: policy name for promotion access.</summary>
    public const string Promote = "SharpNinjaAdminPromote";

    /// <summary>TR-9: policy name for product key administration access.</summary>
    public const string KeyAdmin = "SharpNinjaAdminKeyAdmin";
}
