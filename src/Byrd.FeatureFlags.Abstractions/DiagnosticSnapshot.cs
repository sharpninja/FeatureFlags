namespace Byrd.FeatureFlags.Abstractions;

/// <summary>TR-10 Phase 0 contract: diagnostic state exposed for host debug menus.</summary>
/// <param name="ProductId">Compile-time product identifier.</param>
/// <param name="ReleaseId">Compile-time release identifier.</param>
/// <param name="ManifestId">Active manifest identifier.</param>
/// <param name="LastUpdated">Last manifest update timestamp.</param>
public sealed record DiagnosticSnapshot(
    string ProductId,
    string ReleaseId,
    string ManifestId,
    DateTimeOffset? LastUpdated);
