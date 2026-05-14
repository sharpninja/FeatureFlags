using System.Globalization;
using SharpNinja.FeatureFlags.Cli;
using Xunit;

namespace SharpNinja.FeatureFlags.Cli.Tests;

/// <summary>FR-12 tests for the flagctl validate command wiring.</summary>
public sealed class FlagctlValidateCommandTests
{
    /// <summary>Help arguments print usage and return success.</summary>
    /// <param name="helpArgument">The help argument to test.</param>
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpArgumentsPrintUsageAndReturnSuccess(string helpArgument)
    {
        var result = RunWithUnexpectedValidator(helpArgument);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: flagctl validate <manifest-path>", result.Output);
        Assert.Empty(result.Error);
    }

    /// <summary>No arguments print usage as a usage error.</summary>
    [Fact]
    public void NoArgumentsPrintUsageAndReturnUsageError()
    {
        var result = RunWithUnexpectedValidator();

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Usage: flagctl validate <manifest-path>", result.Error);
    }

    /// <summary>Missing manifest paths fail before invoking validation.</summary>
    [Fact]
    public void MissingManifestPathReturnsUsageError()
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var result = RunWithUnexpectedValidator("validate", manifestPath);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Equal($"error: manifest file not found: {manifestPath}{Environment.NewLine}", result.Error);
    }

    /// <summary>Valid manifest validation results print a concise success line.</summary>
    [Fact]
    public void ValidManifestPrintsSuccessAndReturnsSuccess()
    {
        var manifestPath = CreateTemporaryManifest(ValidManifest);
        try
        {
            var result = Run(
                ["validate", manifestPath],
                path =>
                {
                    Assert.Equal(manifestPath, path);
                    return new ManifestValidationSummary(true, []);
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal($"valid: {manifestPath}{Environment.NewLine}", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Invalid manifest validation results print deterministic sorted error lines.</summary>
    [Fact]
    public void InvalidManifestPrintsDeterministicErrorsAndReturnsInvalid()
    {
        var manifestPath = CreateTemporaryManifest("{}");
        try
        {
            var result = Run(
                ["validate", manifestPath],
                _ => new ManifestValidationSummary(
                    false,
                    [
                        new ManifestValidationDiagnostic("ZZZ", "last", "$.z"),
                        new ManifestValidationDiagnostic("AAA", "middle", "$.a"),
                        new ManifestValidationDiagnostic("AAA", "first", "$.a"),
                    ]));

            var expectedError = string.Join(
                Environment.NewLine,
                [
                    "error AAA $.a: first",
                    "error AAA $.a: middle",
                    "error ZZZ $.z: last",
                    string.Empty,
                ]);

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Equal(expectedError, result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    private static CommandResult RunWithUnexpectedValidator(params string[] args)
    {
        return Run(
            args,
            _ => throw new InvalidOperationException("The manifest validator should not be called."));
    }

    private static CommandResult Run(
        string[] args,
        Func<string, ManifestValidationSummary> validateFile)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = FlagctlCommandRunner.Run(args, output, error, validateFile);

        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static string CreateTemporaryManifest(string content)
    {
        var manifestPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(manifestPath, content);
        return manifestPath;
    }

    private const string ValidManifest =
        """
        {
          "schemaVersion": 1,
          "productId": "truckmate",
          "releaseId": "2026.05",
          "environment": "Development",
          "flags": [
            {
              "key": "search.enabled",
              "type": "boolean",
              "defaultValue": true,
              "killable": true,
              "productScope": [ "truckmate" ]
            }
          ]
        }
        """;

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
