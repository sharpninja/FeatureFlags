namespace Byrd.FeatureFlags.Cli;

internal sealed record ManifestValidationSummary(
    bool IsValid,
    IReadOnlyCollection<ManifestValidationDiagnostic> Errors);
