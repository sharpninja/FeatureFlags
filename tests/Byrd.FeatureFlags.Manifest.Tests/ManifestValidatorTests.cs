using Byrd.FeatureFlags.Manifest;
using Xunit;

namespace Byrd.FeatureFlags.Manifest.Tests;

/// <summary>FR-12 Phase 1 contract tests for manifest validation.</summary>
public sealed class ManifestValidatorTests
{
    /// <summary>Valid manifest JSON returns no validation errors.</summary>
    [Fact]
    public void ValidateJsonAcceptsValidManifest()
    {
        ManifestValidationResult result = ManifestValidator.ValidateJson(ValidManifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>Valid manifest files are read and validated through the file API.</summary>
    [Fact]
    public void ValidateFileAcceptsValidManifest()
    {
        string path = Path.Combine(Path.GetTempPath(), string.Concat(Guid.NewGuid().ToString("N"), ".json"));

        try
        {
            File.WriteAllText(path, ValidManifest);

            ManifestValidationResult result = ManifestValidator.ValidateFile(path);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Missing root fields produce deterministic errors in root contract order.</summary>
    [Fact]
    public void ValidateJsonReportsMissingRootFields()
    {
        ManifestValidationResult result = ManifestValidator.ValidateJson("{}");

        Assert.False(result.IsValid);
        Assert.Collection(
            result.Errors,
            error => AssertError(error, "FFMANIFEST_REQUIRED_FIELD", "$.schemaVersion"),
            error => AssertError(error, "FFMANIFEST_REQUIRED_FIELD", "$.productId"),
            error => AssertError(error, "FFMANIFEST_REQUIRED_FIELD", "$.releaseId"),
            error => AssertError(error, "FFMANIFEST_REQUIRED_FIELD", "$.environment"),
            error => AssertError(error, "FFMANIFEST_REQUIRED_FIELD", "$.flags"));
    }

    /// <summary>Duplicate flag keys are rejected at the duplicate flag entry.</summary>
    [Fact]
    public void ValidateJsonReportsDuplicateFlagKeys()
    {
        ManifestValidationResult result = ManifestValidator.ValidateJson(
            ManifestWithFlags(
                """
                {
                  "key": "search.enabled",
                  "type": "boolean",
                  "defaultValue": true,
                  "killable": true,
                  "productScope": [ "truckmate" ]
                },
                {
                  "key": "search.enabled",
                  "type": "boolean",
                  "defaultValue": false,
                  "killable": false,
                  "productScope": [ "truckmate" ]
                }
                """));

        Assert.False(result.IsValid);
        ManifestValidationError error = Assert.Single(result.Errors);
        AssertError(error, "FFMANIFEST_DUPLICATE_KEY", "$.flags[1].key");
    }

    /// <summary>Flag default values must match the declared flag type.</summary>
    [Fact]
    public void ValidateJsonReportsDefaultTypeMismatch()
    {
        ManifestValidationResult result = ManifestValidator.ValidateJson(
            ManifestWithFlags(
                """
                {
                  "key": "search.limit",
                  "type": "integer",
                  "defaultValue": "ten",
                  "killable": true,
                  "productScope": [ "truckmate" ]
                }
                """));

        Assert.False(result.IsValid);
        ManifestValidationError error = Assert.Single(result.Errors);
        AssertError(error, "FFMANIFEST_DEFAULT_VALUE_TYPE", "$.flags[0].defaultValue");
    }

    /// <summary>Product scope must include the root product id.</summary>
    [Fact]
    public void ValidateJsonReportsProductScopeViolation()
    {
        ManifestValidationResult result = ManifestValidator.ValidateJson(
            ManifestWithFlags(
                """
                {
                  "key": "search.enabled",
                  "type": "boolean",
                  "defaultValue": true,
                  "killable": true,
                  "productScope": [ "other-product" ]
                }
                """));

        Assert.False(result.IsValid);
        ManifestValidationError error = Assert.Single(result.Errors);
        AssertError(error, "FFMANIFEST_PRODUCT_SCOPE_PRODUCT", "$.flags[0].productScope");
    }

    /// <summary>Rule override values must match the declared flag type.</summary>
    [Fact]
    public void ValidateJsonReportsRuleValueTypeMismatch()
    {
        ManifestValidationResult result = ManifestValidator.ValidateJson(
            ManifestWithFlags(
                """
                {
                  "key": "search.enabled",
                  "type": "boolean",
                  "defaultValue": true,
                  "killable": true,
                  "productScope": [ "truckmate" ],
                  "rules": [
                    {
                      "when": "user.region == 'us'",
                      "value": "yes"
                    }
                  ]
                }
                """));

        Assert.False(result.IsValid);
        ManifestValidationError error = Assert.Single(result.Errors);
        AssertError(error, "FFMANIFEST_RULE_VALUE_TYPE", "$.flags[0].rules[0].value");
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
              "productScope": [ "truckmate" ],
              "rules": [
                {
                  "when": "user.region == 'us'",
                  "value": false
                }
              ]
            },
            {
              "key": "search.title",
              "type": "string",
              "defaultValue": "Search",
              "killable": false,
              "productScope": [ "truckmate", "dispatch" ]
            },
            {
              "key": "search.limit",
              "type": "integer",
              "defaultValue": 10,
              "killable": false,
              "productScope": [ "truckmate" ]
            },
            {
              "key": "search.weight",
              "type": "number",
              "defaultValue": 0.75,
              "killable": false,
              "productScope": [ "truckmate" ]
            }
          ]
        }
        """;

    private static string ManifestWithFlags(string flags) =>
        string.Concat(
            """
            {
              "schemaVersion": 1,
              "productId": "truckmate",
              "releaseId": "2026.05",
              "environment": "Production",
              "flags": [
            """,
            Environment.NewLine,
            flags,
            Environment.NewLine,
            """
              ]
            }
            """);

    private static void AssertError(ManifestValidationError error, string code, string path)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(path, error.Path);
    }
}
