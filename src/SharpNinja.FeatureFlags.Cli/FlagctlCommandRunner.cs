using System.Globalization;

namespace SharpNinja.FeatureFlags.Cli;

internal static class FlagctlCommandRunner
{
    private const string ValidateCommand = "validate";

    private const string UsageText = """
        Usage: flagctl validate <manifest-path> [options]

        Commands:
          validate <manifest-path>  Validate one JSON feature-flag manifest.

        Options:
          --product-id <id>         Require the manifest productId to match the build ProductId.
          --release-id <id>         Require the manifest releaseId to match the build ReleaseId.
          --schema-version <value>  Supported manifest schema version. Defaults to 1.
          --public-key <path>       Validate embedded Ed25519 public-key material.
          --generated-bindings <path>
                                    Validate generated accessor/gate binding diagnostics input.
          -h, --help                Show usage.
        """;

    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        return Run(
            args,
            output,
            error,
            (Func<ManifestValidationRequest, ManifestValidationSummary>)ValidateWithManifestApi);
    }

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        Func<string, ManifestValidationSummary> validateFile)
    {
        ArgumentNullException.ThrowIfNull(validateFile);

        return Run(
            args,
            output,
            error,
            request => validateFile(request.ManifestPath));
    }

    internal static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        Func<ManifestValidationRequest, ManifestValidationSummary> validateFile)
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

        if (args.Count < 2)
        {
            error.WriteLine("error: validate expects one manifest path.");
            error.WriteLine(UsageText);
            return 2;
        }

        var manifestPath = args[1];
        if (!TryParseValidateOptions(args, error, out ManifestValidationRequest? request))
        {
            return 2;
        }

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
            validationResult = validateFile(request);
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

    private static bool TryParseValidateOptions(
        IReadOnlyList<string> args,
        TextWriter error,
        out ManifestValidationRequest request)
    {
        request = null!;
        string manifestPath = args[1];
        string? productId = null;
        string? releaseId = null;
        string? publicKeyPath = null;
        int schemaVersion = 1;
        List<string> generatedBindingPaths = [];

        for (int index = 2; index < args.Count; index++)
        {
            string option = args[index];
            if (IsHelpArgument(option))
            {
                error.WriteLine("error: help must be requested before validate options.");
                error.WriteLine(UsageText);
                return false;
            }

            switch (option)
            {
                case "--product-id":
                    if (!TryReadOptionValue(args, ref index, option, error, out string productIdValue))
                    {
                        return false;
                    }

                    productId = productIdValue;
                    break;
                case "--release-id":
                    if (!TryReadOptionValue(args, ref index, option, error, out string releaseIdValue))
                    {
                        return false;
                    }

                    releaseId = releaseIdValue;
                    break;
                case "--schema-version":
                    if (!TryReadOptionValue(args, ref index, option, error, out string schemaVersionText)
                        || !int.TryParse(schemaVersionText, NumberStyles.None, CultureInfo.InvariantCulture, out schemaVersion)
                        || schemaVersion <= 0)
                    {
                        error.WriteLine("error: --schema-version expects a positive integer.");
                        return false;
                    }

                    break;
                case "--public-key":
                    if (!TryReadOptionValue(args, ref index, option, error, out string publicKeyPathValue))
                    {
                        return false;
                    }

                    publicKeyPath = publicKeyPathValue;
                    break;
                case "--generated-bindings":
                    if (!TryReadOptionValue(args, ref index, option, error, out string generatedBindingPath))
                    {
                        return false;
                    }

                    generatedBindingPaths.Add(generatedBindingPath);
                    break;
                default:
                    error.WriteLine($"error: unknown option '{option}'.");
                    error.WriteLine(UsageText);
                    return false;
            }
        }

        request = new ManifestValidationRequest(
            manifestPath,
            productId,
            releaseId,
            publicKeyPath,
            schemaVersion,
            generatedBindingPaths);

        return true;
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        TextWriter error,
        out string value)
    {
        value = string.Empty;
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            error.WriteLine($"error: {option} expects a value.");
            return false;
        }

        value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            error.WriteLine($"error: {option} expects a non-empty value.");
            return false;
        }

        return true;
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

    private static ManifestValidationSummary ValidateWithManifestApi(ManifestValidationRequest request)
    {
        return FlagctlManifestValidator.Validate(request);
    }
}
