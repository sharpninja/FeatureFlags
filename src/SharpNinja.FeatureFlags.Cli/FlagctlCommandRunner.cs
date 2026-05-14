using SharpNinja.FeatureFlags.Manifest;

namespace SharpNinja.FeatureFlags.Cli;

internal static class FlagctlCommandRunner
{
    private const string ValidateCommand = "validate";

    private const string UsageText = """
        Usage: flagctl validate <manifest-path>

        Commands:
          validate <manifest-path>  Validate one JSON feature-flag manifest.

        Options:
          -h, --help                Show usage.
        """;

    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        return Run(args, output, error, ValidateWithManifestApi);
    }

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        Func<string, ManifestValidationSummary> validateFile)
    {
        if (args.Count == 0)
        {
            error.WriteLine(UsageText);
            return 2;
        }

        if (args.Count == 1 && IsHelpArgument(args[0]))
        {
            output.WriteLine(UsageText);
            return 0;
        }

        if (!StringComparer.Ordinal.Equals(args[0], ValidateCommand))
        {
            error.WriteLine($"error: unknown command '{args[0]}'.");
            error.WriteLine(UsageText);
            return 2;
        }

        if (args.Count == 2 && IsHelpArgument(args[1]))
        {
            output.WriteLine(UsageText);
            return 0;
        }

        if (args.Count != 2)
        {
            error.WriteLine("error: validate expects one manifest path.");
            error.WriteLine(UsageText);
            return 2;
        }

        var manifestPath = args[1];
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            error.WriteLine("error: manifest path is required.");
            return 2;
        }

        if (Directory.Exists(manifestPath))
        {
            error.WriteLine($"error: manifest path is a directory: {manifestPath}");
            return 2;
        }

        if (!File.Exists(manifestPath))
        {
            error.WriteLine($"error: manifest file not found: {manifestPath}");
            return 2;
        }

        if (!TryConfirmReadable(manifestPath, error))
        {
            return 2;
        }

        ManifestValidationSummary validationResult;
        try
        {
            validationResult = validateFile(manifestPath);
        }
        catch (ArgumentException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return 2;
        }
        catch (IOException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return 2;
        }
        catch (NotSupportedException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return 2;
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return 2;
        }

        if (validationResult.Errors.Any(IsFileReadError))
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return 2;
        }

        if (validationResult.IsValid)
        {
            output.WriteLine($"valid: {manifestPath}");
            return 0;
        }

        if (validationResult.Errors.Count == 0)
        {
            error.WriteLine("error: manifest is invalid.");
            return 1;
        }

        foreach (var validationError in validationResult.Errors
            .OrderBy(static item => item.Path, StringComparer.Ordinal)
            .ThenBy(static item => item.Code, StringComparer.Ordinal)
            .ThenBy(static item => item.Message, StringComparer.Ordinal))
        {
            var errorPath = string.IsNullOrWhiteSpace(validationError.Path) ? "$" : validationError.Path;
            error.WriteLine($"error {validationError.Code} {errorPath}: {validationError.Message}");
        }

        return 1;
    }

    private static bool IsHelpArgument(string argument)
    {
        return StringComparer.Ordinal.Equals(argument, "--help")
            || StringComparer.Ordinal.Equals(argument, "-h");
    }

    private static bool IsFileReadError(ManifestValidationDiagnostic diagnostic)
    {
        return StringComparer.Ordinal.Equals(diagnostic.Code, "FFMANIFEST_FILE_READ");
    }

    private static bool TryConfirmReadable(string manifestPath, TextWriter error)
    {
        try
        {
            using var stream = File.Open(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (ArgumentException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return false;
        }
        catch (IOException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return false;
        }
        catch (NotSupportedException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine($"error: manifest file is unreadable: {manifestPath}");
            return false;
        }
    }

    private static ManifestValidationSummary ValidateWithManifestApi(string path)
    {
        var result = ManifestValidator.ValidateFile(path);
        var errors = result.Errors
            .Select(static validationError => new ManifestValidationDiagnostic(
                validationError.Code ?? string.Empty,
                validationError.Message ?? string.Empty,
                validationError.Path ?? string.Empty))
            .ToArray();

        return new ManifestValidationSummary(result.IsValid, errors);
    }
}
