namespace SharpNinja.FeatureFlags.Admin.Data;

/// <summary>FR-9 FR-11 TR-11: Pairs data-provider metadata with its configured options.</summary>
/// <remarks>
/// Immutable value; equality is structural; safe to share across threads.
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-9"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-11"/>
/// <see href="https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-11"/>
/// </remarks>
/// <param name="Descriptor">The registered data-provider descriptor.</param>
/// <param name="Options">The registered data-provider options.</param>
public sealed record AdminDataProviderRegistration(
    AdminDataProviderDescriptor Descriptor,
    AdminDataProviderOptions Options);
