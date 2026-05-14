namespace SharpNinja.FeatureFlags.Cli;

internal sealed record ManifestValidationRequest(
    string ManifestPath,
    string? ProductId,
    string? ReleaseId,
    string? PublicKeyPath,
    int SchemaVersion,
    IReadOnlyCollection<string> GeneratedBindingPaths);
