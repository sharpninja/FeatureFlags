using System.Globalization;
using SharpNinja.FeatureFlags.Cli;
using Xunit;

namespace SharpNinja.FeatureFlags.Cli.Tests;

/// <summary>FR-12 TR-8 tests for the flagctl validate command wiring.</summary>
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

    /// <summary>Signed manifests with matching build identity, public key, and v1 CEL pass real validation.</summary>
    [Fact]
    public void ValidateAcceptsSignedManifestWithBuildIdentityAndPublicKey()
    {
        var manifestPath = CreateTemporaryManifest(ValidSignedManifest);
        var publicKeyPath = CreateTemporaryFile(Convert.ToBase64String(new byte[32]));
        try
        {
            var result = RunDefault(
                "validate",
                manifestPath,
                "--product-id",
                "truckmate",
                "--release-id",
                "2026.05",
                "--schema-version",
                "1",
                "--public-key",
                publicKeyPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal($"valid: {manifestPath}{Environment.NewLine}", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(publicKeyPath);
        }
    }

    /// <summary>Build identity options fail manifests that do not match the binary ProductId/ReleaseId tuple.</summary>
    [Fact]
    public void ValidateReportsProductAndReleaseMismatch()
    {
        var manifestPath = CreateTemporaryManifest(ValidSignedManifest);
        try
        {
            var result = RunDefault(
                "validate",
                manifestPath,
                "--product-id",
                "drivermate",
                "--release-id",
                "2026.06");

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains("FFMANIFEST_PRODUCT_MISMATCH $.productId", result.Error);
            Assert.Contains("FFMANIFEST_RELEASE_MISMATCH $.releaseId", result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Rule predicates are validated against the supported v1 CEL subset.</summary>
    [Fact]
    public void ValidateReportsUnsupportedCelFunction()
    {
        var manifestPath = CreateTemporaryManifest(SignedManifestWithRule("regex('a')"));
        try
        {
            var result = RunDefault("validate", manifestPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains("FFMANIFEST_CEL_SYNTAX $.flags[0].rules[0].when", result.Error);
            Assert.Contains("CEL function 'regex' is not supported in v1.", result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    /// <summary>Signature metadata must use Ed25519 and valid key/signature material.</summary>
    [Fact]
    public void ValidateReportsInvalidSignatureAndPublicKeyFormat()
    {
        var manifestPath = CreateTemporaryManifest(SignedManifestWithSignature("RSA", "not-base64"));
        var publicKeyPath = CreateTemporaryFile("not-base64");
        try
        {
            var result = RunDefault("validate", manifestPath, "--public-key", publicKeyPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains("FFMANIFEST_SIGNATURE_ALGORITHM $.signature.algorithm", result.Error);
            Assert.Contains("FFMANIFEST_SIGNATURE_VALUE $.signature.value", result.Error);
            Assert.Contains("FFMANIFEST_PUBLIC_KEY_FORMAT", result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(publicKeyPath);
        }
    }

    /// <summary>Generated accessor/gate binding files surface SNFF diagnostics against the manifest.</summary>
    [Fact]
    public void ValidateReportsGeneratedBindingDiagnostics()
    {
        var manifestPath = CreateTemporaryManifest(ValidSignedManifest);
        var bindingsPath = CreateTemporaryFile(
            """
            {
              "bindings": [
                {
                  "kind": "gate",
                  "flagKey": "search.title",
                  "productId": "truckmate"
                },
                {
                  "kind": "accessor",
                  "flagKey": "search.enabled",
                  "valueType": "string",
                  "productId": "drivermate"
                },
                {
                  "kind": "accessor",
                  "flagKey": "missing.flag",
                  "valueType": "bool",
                  "productId": "truckmate"
                }
              ]
            }
            """);
        try
        {
            var result = RunDefault("validate", manifestPath, "--generated-bindings", bindingsPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains("SNFF0001", result.Error);
            Assert.Contains("SNFF0002", result.Error);
            Assert.Contains("SNFF0003", result.Error);
        }
        finally
        {
            File.Delete(manifestPath);
            File.Delete(bindingsPath);
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

    private static CommandResult RunDefault(params string[] args)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = FlagctlCommandRunner.Run(args, output, error);

        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private static string CreateTemporaryManifest(string content)
    {
        return CreateTemporaryFile(content, ".json");
    }

    private static string CreateTemporaryFile(string content, string extension = ".tmp")
    {
        var path = Path.Combine(Path.GetTempPath(), string.Concat(Guid.NewGuid().ToString("N"), extension));
        File.WriteAllText(path, content);
        return path;
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

    private static readonly string ValidSignedManifest = SignedManifestWithRule("ProductId == 'truckmate'");

    private static string SignedManifestWithRule(string rule) =>
        string.Concat(
            """
            {
              "schemaVersion": 1,
              "productId": "truckmate",
              "releaseId": "2026.05",
              "environment": "Development",
              "signature": {
                "algorithm": "Ed25519",
                "keyId": "test-key",
                "value": "
            """,
            Convert.ToBase64String(new byte[64]),
            """
            "
              },
              "flags": [
                {
                  "key": "search.enabled",
                  "type": "boolean",
                  "defaultValue": true,
                  "killable": true,
                  "productScope": [ "truckmate" ],
                  "rules": [
                    {
                      "when": "
            """,
            rule,
            """
            ",
                      "value": false
                    }
                  ]
                },
                {
                  "key": "search.title",
                  "type": "string",
                  "defaultValue": "Search",
                  "killable": false,
                  "productScope": [ "truckmate" ]
                }
              ]
            }
            """);

    private static string SignedManifestWithSignature(string algorithm, string signatureValue) =>
        string.Concat(
            """
            {
              "schemaVersion": 1,
              "productId": "truckmate",
              "releaseId": "2026.05",
              "environment": "Development",
              "signature": {
                "algorithm": "
            """,
            algorithm,
            """
            ",
                "keyId": "test-key",
                "value": "
            """,
            signatureValue,
            """
            "
              },
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
            """);

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
