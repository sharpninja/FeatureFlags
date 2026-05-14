namespace SharpNinja.FeatureFlags.Admin.Data;

/// <summary>FR-9 FR-11 TR-11: Pairs data-provider metadata with its configured options.</summary>
/// <param name="Descriptor">The registered data-provider descriptor.</param>
/// <param name="Options">The registered data-provider options.</param>
public sealed record AdminDataProviderRegistration(
    AdminDataProviderDescriptor Descriptor,
    AdminDataProviderOptions Options);
