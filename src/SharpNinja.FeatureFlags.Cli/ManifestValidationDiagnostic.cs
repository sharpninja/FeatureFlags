namespace SharpNinja.FeatureFlags.Cli;

internal sealed record ManifestValidationDiagnostic(string Code, string Message, string Path);
