using System.Globalization;
using System.Text.Json;
using SharpNinja.FeatureFlags.Evaluation;

namespace SharpNinja.FeatureFlags.Manifest;

/// <summary>FR-12 Phase 1 contract: result returned by manifest schema validation.</summary>
/// <param name="IsValid">Indicates whether the manifest passed validation.</param>
/// <param name="Errors">Validation errors in deterministic order.</param>
public sealed record ManifestValidationResult(bool IsValid, IReadOnlyList<ManifestValidationError> Errors);

/// <summary>FR-12 Phase 1 contract: describes one manifest schema validation failure.</summary>
/// <param name="Code">Stable machine-readable error code.</param>
/// <param name="Message">Human-readable validation failure message.</param>
/// <param name="Path">Optional JSON path for the validation failure.</param>
public sealed record ManifestValidationError(string Code, string Message, string? Path = null);

/// <summary>FR-12 Phase 1 contract: validates feature flag manifest JSON for CI and CLI use.</summary>
public static class ManifestValidator
{
    private const string InvalidJsonCode = "FFMANIFEST_JSON_INVALID";
    private const string RootObjectCode = "FFMANIFEST_ROOT_OBJECT";
    private const string RequiredFieldCode = "FFMANIFEST_REQUIRED_FIELD";
    private const string SchemaVersionCode = "FFMANIFEST_SCHEMA_VERSION";
    private const string StringRequiredCode = "FFMANIFEST_STRING_REQUIRED";
    private const string EnvironmentCode = "FFMANIFEST_ENVIRONMENT";
    private const string FlagsArrayCode = "FFMANIFEST_FLAGS_ARRAY";
    private const string FlagObjectCode = "FFMANIFEST_FLAG_OBJECT";
    private const string DuplicateFlagKeyCode = "FFMANIFEST_DUPLICATE_KEY";
    private const string FlagTypeCode = "FFMANIFEST_FLAG_TYPE";
    private const string DefaultValueTypeCode = "FFMANIFEST_DEFAULT_VALUE_TYPE";
    private const string BooleanRequiredCode = "FFMANIFEST_BOOLEAN_REQUIRED";
    private const string ProductScopeArrayCode = "FFMANIFEST_PRODUCT_SCOPE_ARRAY";
    private const string ProductScopeEmptyCode = "FFMANIFEST_PRODUCT_SCOPE_EMPTY";
    private const string ProductScopeItemCode = "FFMANIFEST_PRODUCT_SCOPE_ITEM";
    private const string ProductScopeProductCode = "FFMANIFEST_PRODUCT_SCOPE_PRODUCT";
    private const string RulesArrayCode = "FFMANIFEST_RULES_ARRAY";
    private const string RuleObjectCode = "FFMANIFEST_RULE_OBJECT";
    private const string RuleValueTypeCode = "FFMANIFEST_RULE_VALUE_TYPE";
    private const string RuleWhenSyntaxCode = "FFMANIFEST_RULE_WHEN_SYNTAX";
    private const string RuleWhenTypeCode = "FFMANIFEST_RULE_WHEN_TYPE";
    private const string RuleSchemaVersionCode = "FFMANIFEST_RULE_SCHEMA_VERSION";
    private const string FileReadCode = "FFMANIFEST_FILE_READ";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>FR-12 validates a manifest JSON payload.</summary>
    /// <param name="json">Manifest JSON text.</param>
    /// <returns>The validation result.</returns>
    public static ManifestValidationResult ValidateJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, JsonOptions);
            return ValidateDocument(document.RootElement);
        }
        catch (JsonException exception)
        {
            return new ManifestValidationResult(
                false,
                new[]
                {
                    new ManifestValidationError(
                        InvalidJsonCode,
                        "Manifest JSON could not be parsed.",
                        exception.Path),
                });
        }
    }

    /// <summary>FR-12 validates a manifest JSON file.</summary>
    /// <param name="path">Manifest JSON file path.</param>
    /// <returns>The validation result.</returns>
    public static ManifestValidationResult ValidateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return ValidateJson(File.ReadAllText(path));
        }
        catch (IOException exception)
        {
            return FileReadFailure(path, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return FileReadFailure(path, exception.Message);
        }
    }

    private static ManifestValidationResult ValidateDocument(JsonElement root)
    {
        List<ManifestValidationError> errors = [];

        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(errors, RootObjectCode, "Manifest root must be a JSON object.", "$");
            return ToResult(errors);
        }

        int? schemaVersion = ValidateSchemaVersion(root, errors);
        string? productId = ValidateRequiredString(root, "productId", "$.productId", errors);
        _ = ValidateRequiredString(root, "releaseId", "$.releaseId", errors);
        string? environment = ValidateRequiredString(root, "environment", "$.environment", errors);
        ValidateEnvironment(environment, errors);
        ValidateFlags(root, productId, schemaVersion, errors);

        return ToResult(errors);
    }

    private static ManifestValidationResult FileReadFailure(string path, string message) =>
        new(
            false,
            new[]
            {
                new ManifestValidationError(
                    FileReadCode,
                    string.Concat("Manifest file could not be read: ", message),
                    path),
            });

    private static int? ValidateSchemaVersion(JsonElement root, List<ManifestValidationError> errors)
    {
        if (!TryGetRequiredProperty(root, "schemaVersion", "$.schemaVersion", errors, out JsonElement schemaVersion))
        {
            return null;
        }

        if (schemaVersion.ValueKind != JsonValueKind.Number
            || !schemaVersion.TryGetInt32(out int version)
            || version != 1)
        {
            Add(errors, SchemaVersionCode, "schemaVersion must be integer 1.", "$.schemaVersion");
            return schemaVersion.ValueKind == JsonValueKind.Number && schemaVersion.TryGetInt32(out version)
                ? version
                : null;
        }

        return version;
    }

    private static string? ValidateRequiredString(
        JsonElement parent,
        string propertyName,
        string path,
        List<ManifestValidationError> errors)
    {
        if (!TryGetRequiredProperty(parent, propertyName, path, errors, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            Add(errors, StringRequiredCode, string.Concat(propertyName, " must be a non-empty string."), path);
            return null;
        }

        string? text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            Add(errors, StringRequiredCode, string.Concat(propertyName, " must be a non-empty string."), path);
            return null;
        }

        return text;
    }

    /// <remarks>
    /// FR-11 design note: The admin plane supports custom environment names for authoring workflows.
    /// Before a draft is published to a manifest, the admin normalizes custom environment names to one
    /// of the three canonical values (Development, Staging, Production). Manifests therefore always
    /// carry one of these three values, and this validator enforces that contract. Custom environment
    /// names never appear in published manifests; they exist only in the admin-plane draft lifecycle.
    /// </remarks>
    private static void ValidateEnvironment(string? environment, List<ManifestValidationError> errors)
    {
        if (environment is null)
        {
            return;
        }

        if (!string.Equals(environment, "Development", StringComparison.Ordinal)
            && !string.Equals(environment, "Staging", StringComparison.Ordinal)
            && !string.Equals(environment, "Production", StringComparison.Ordinal))
        {
            Add(
                errors,
                EnvironmentCode,
                "environment must be Development, Staging, or Production.",
                "$.environment");
        }
    }

    private static void ValidateFlags(
        JsonElement root,
        string? productId,
        int? schemaVersion,
        List<ManifestValidationError> errors)
    {
        if (!TryGetRequiredProperty(root, "flags", "$.flags", errors, out JsonElement flags))
        {
            return;
        }

        if (flags.ValueKind != JsonValueKind.Array)
        {
            Add(errors, FlagsArrayCode, "flags must be an array.", "$.flags");
            return;
        }

        Dictionary<string, int> seenKeys = new(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement flag in flags.EnumerateArray())
        {
            ValidateFlag(flag, index, productId, schemaVersion, seenKeys, errors);
            index++;
        }
    }

    private static void ValidateFlag(
        JsonElement flag,
        int index,
        string? productId,
        int? schemaVersion,
        Dictionary<string, int> seenKeys,
        List<ManifestValidationError> errors)
    {
        string flagPath = FlagPath(index);
        if (flag.ValueKind != JsonValueKind.Object)
        {
            Add(errors, FlagObjectCode, "Each flags item must be an object.", flagPath);
            return;
        }

        string? key = ValidateRequiredString(flag, "key", string.Concat(flagPath, ".key"), errors);
        if (key is not null && !seenKeys.TryAdd(key, index))
        {
            Add(
                errors,
                DuplicateFlagKeyCode,
                string.Concat("Flag key '", key, "' must be unique."),
                string.Concat(flagPath, ".key"));
        }

        string? type = ValidateFlagType(flag, flagPath, errors);

        if (TryGetRequiredProperty(
                flag,
                "defaultValue",
                string.Concat(flagPath, ".defaultValue"),
                errors,
                out JsonElement defaultValue)
            && type is not null
            && !JsonKindMatchesType(defaultValue, type))
        {
            Add(
                errors,
                DefaultValueTypeCode,
                string.Concat("defaultValue must match flag type '", type, "'."),
                string.Concat(flagPath, ".defaultValue"));
        }

        ValidateRequiredBoolean(flag, "killable", string.Concat(flagPath, ".killable"), errors);
        ValidateProductScope(flag, flagPath, productId, errors);
        ValidateRules(flag, flagPath, type, schemaVersion, errors);
    }

    private static string? ValidateFlagType(JsonElement flag, string flagPath, List<ManifestValidationError> errors)
    {
        string path = string.Concat(flagPath, ".type");
        string? type = ValidateRequiredString(flag, "type", path, errors);
        if (type is null)
        {
            return null;
        }

        if (!IsKnownType(type))
        {
            Add(errors, FlagTypeCode, "type must be boolean, string, integer, or number.", path);
            return null;
        }

        return type;
    }

    private static void ValidateRequiredBoolean(
        JsonElement parent,
        string propertyName,
        string path,
        List<ManifestValidationError> errors)
    {
        if (!TryGetRequiredProperty(parent, propertyName, path, errors, out JsonElement value))
        {
            return;
        }

        if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            Add(errors, BooleanRequiredCode, string.Concat(propertyName, " must be boolean."), path);
        }
    }

    private static void ValidateProductScope(
        JsonElement flag,
        string flagPath,
        string? productId,
        List<ManifestValidationError> errors)
    {
        string path = string.Concat(flagPath, ".productScope");
        if (!TryGetRequiredProperty(flag, "productScope", path, errors, out JsonElement productScope))
        {
            return;
        }

        if (productScope.ValueKind != JsonValueKind.Array)
        {
            Add(errors, ProductScopeArrayCode, "productScope must be a non-empty array of non-empty strings.", path);
            return;
        }

        int count = 0;
        bool includesProduct = false;
        foreach (JsonElement item in productScope.EnumerateArray())
        {
            string itemPath = ProductScopeItemPath(flagPath, count);
            count++;

            if (item.ValueKind != JsonValueKind.String)
            {
                Add(errors, ProductScopeItemCode, "productScope entries must be non-empty strings.", itemPath);
                continue;
            }

            string? value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                Add(errors, ProductScopeItemCode, "productScope entries must be non-empty strings.", itemPath);
                continue;
            }

            if (productId is not null && string.Equals(value, productId, StringComparison.Ordinal))
            {
                includesProduct = true;
            }
        }

        if (count == 0)
        {
            Add(errors, ProductScopeEmptyCode, "productScope must contain at least one product id.", path);
        }

        if (productId is not null && !includesProduct)
        {
            Add(errors, ProductScopeProductCode, "productScope must include the root productId.", path);
        }
    }

    private static void ValidateRules(
        JsonElement flag,
        string flagPath,
        string? type,
        int? schemaVersion,
        List<ManifestValidationError> errors)
    {
        if (!flag.TryGetProperty("rules", out JsonElement rules))
        {
            return;
        }

        string path = string.Concat(flagPath, ".rules");
        if (rules.ValueKind != JsonValueKind.Array)
        {
            Add(errors, RulesArrayCode, "rules must be an array when present.", path);
            return;
        }

        if (schemaVersion is not null && schemaVersion != 1)
        {
            Add(errors, RuleSchemaVersionCode, "rules require manifest schemaVersion 1.", path);
        }

        int index = 0;
        foreach (JsonElement rule in rules.EnumerateArray())
        {
            ValidateRule(rule, RulePath(flagPath, index), type, errors);
            index++;
        }
    }

    private static void ValidateRule(
        JsonElement rule,
        string rulePath,
        string? type,
        List<ManifestValidationError> errors)
    {
        if (rule.ValueKind != JsonValueKind.Object)
        {
            Add(errors, RuleObjectCode, "Each rules item must be an object.", rulePath);
            return;
        }

        string? when = ValidateRequiredString(rule, "when", string.Concat(rulePath, ".when"), errors);
        if (when is not null)
        {
            ValidateRulePredicate(when, string.Concat(rulePath, ".when"), errors);
        }

        if (TryGetRequiredProperty(
                rule,
                "value",
                string.Concat(rulePath, ".value"),
                errors,
                out JsonElement value)
            && type is not null
            && !JsonKindMatchesType(value, type))
        {
            Add(
                errors,
                RuleValueTypeCode,
                string.Concat("rule value must match flag type '", type, "'."),
                string.Concat(rulePath, ".value"));
        }
    }

    private static void ValidateRulePredicate(string when, string path, List<ManifestValidationError> errors)
    {
        RulePredicateValidationResult result = RulePredicateValidator.Validate(when);
        foreach (RulePredicateDiagnostic diagnostic in result.Diagnostics)
        {
            string code = string.Equals(diagnostic.Code, "FFCEL_TYPE", StringComparison.Ordinal)
                ? RuleWhenTypeCode
                : RuleWhenSyntaxCode;

            Add(
                errors,
                code,
                string.Concat(
                    "rule when predicate is invalid at character ",
                    diagnostic.Position.ToString(CultureInfo.InvariantCulture),
                    ": ",
                    diagnostic.Message),
                path);
        }
    }

    private static bool TryGetRequiredProperty(
        JsonElement parent,
        string propertyName,
        string path,
        List<ManifestValidationError> errors,
        out JsonElement value)
    {
        if (parent.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        Add(errors, RequiredFieldCode, string.Concat(propertyName, " is required."), path);
        return false;
    }

    private static bool IsKnownType(string type) =>
        string.Equals(type, "boolean", StringComparison.Ordinal)
        || string.Equals(type, "string", StringComparison.Ordinal)
        || string.Equals(type, "integer", StringComparison.Ordinal)
        || string.Equals(type, "number", StringComparison.Ordinal);

    private static bool JsonKindMatchesType(JsonElement value, string type) =>
        type switch
        {
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            _ => false,
        };

    private static string FlagPath(int index) =>
        string.Concat("$.flags[", index.ToString(CultureInfo.InvariantCulture), "]");

    private static string ProductScopeItemPath(string flagPath, int index) =>
        string.Concat(flagPath, ".productScope[", index.ToString(CultureInfo.InvariantCulture), "]");

    private static string RulePath(string flagPath, int index) =>
        string.Concat(flagPath, ".rules[", index.ToString(CultureInfo.InvariantCulture), "]");

    private static ManifestValidationResult ToResult(List<ManifestValidationError> errors) =>
        new(errors.Count == 0, errors.ToArray());

    private static void Add(List<ManifestValidationError> errors, string code, string message, string? path) =>
        errors.Add(new ManifestValidationError(code, message, path));
}
