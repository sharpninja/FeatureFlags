using System.Globalization;
using System.Text.Json;
using SharpNinja.FeatureFlags.Manifest;

namespace SharpNinja.FeatureFlags.Cli;

internal static class FlagctlManifestValidator
{
    private const string CelSyntaxCode = "FFMANIFEST_CEL_SYNTAX";
    private const string ProductMismatchCode = "FFMANIFEST_PRODUCT_MISMATCH";
    private const string ReleaseMismatchCode = "FFMANIFEST_RELEASE_MISMATCH";
    private const string SchemaCompatibilityCode = "FFMANIFEST_SCHEMA_COMPATIBILITY";
    private const string SignatureRequiredCode = "FFMANIFEST_SIGNATURE_REQUIRED";
    private const string SignatureObjectCode = "FFMANIFEST_SIGNATURE_OBJECT";
    private const string SignatureAlgorithmCode = "FFMANIFEST_SIGNATURE_ALGORITHM";
    private const string SignatureKeyIdCode = "FFMANIFEST_SIGNATURE_KEYID";
    private const string SignatureValueCode = "FFMANIFEST_SIGNATURE_VALUE";
    private const string PublicKeyReadCode = "FFMANIFEST_PUBLIC_KEY_READ";
    private const string PublicKeyFormatCode = "FFMANIFEST_PUBLIC_KEY_FORMAT";
    private const string BindingJsonInvalidCode = "SNFF_BINDING_JSON_INVALID";
    private const string BindingSchemaCode = "SNFF_BINDING_SCHEMA";
    private const string MissingFlagCode = "SNFF0001";
    private const string TypeMismatchCode = "SNFF0002";
    private const string ProductScopeCode = "SNFF0003";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    internal static ManifestValidationSummary Validate(ManifestValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ManifestValidationResult baseResult = ManifestValidator.ValidateFile(request.ManifestPath);
        List<ManifestValidationDiagnostic> diagnostics = baseResult.Errors
            .Select(static validationError => new ManifestValidationDiagnostic(
                validationError.Code ?? string.Empty,
                validationError.Message ?? string.Empty,
                validationError.Path ?? string.Empty))
            .ToList();

        if (diagnostics.Any(static diagnostic =>
                string.Equals(diagnostic.Code, "FFMANIFEST_FILE_READ", StringComparison.Ordinal)))
        {
            return ToSummary(diagnostics);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(request.ManifestPath), JsonOptions);
        }
        catch (JsonException)
        {
            return ToSummary(diagnostics);
        }
        catch (IOException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                "FFMANIFEST_FILE_READ",
                string.Concat("Manifest file could not be read: ", exception.Message),
                request.ManifestPath));
            return ToSummary(diagnostics);
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                "FFMANIFEST_FILE_READ",
                string.Concat("Manifest file could not be read: ", exception.Message),
                request.ManifestPath));
            return ToSummary(diagnostics);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ToSummary(diagnostics);
            }

            Dictionary<string, FlagInfo> flags = ReadFlags(root);
            ValidateBuildIdentity(root, request, diagnostics);
            ValidateSchemaCompatibility(root, request.SchemaVersion, diagnostics);
            ValidateRules(root, diagnostics);
            ValidateSignature(root, request.PublicKeyPath, diagnostics);
            ValidateGeneratedBindings(request, flags, diagnostics);
        }

        return ToSummary(diagnostics);
    }

    private static void ValidateBuildIdentity(
        JsonElement root,
        ManifestValidationRequest request,
        List<ManifestValidationDiagnostic> diagnostics)
    {
        string? productId = ReadString(root, "productId");
        if (!string.IsNullOrWhiteSpace(request.ProductId)
            && productId is not null
            && !string.Equals(productId, request.ProductId, StringComparison.Ordinal))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                ProductMismatchCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"manifest productId '{productId}' does not match build ProductId '{request.ProductId}'."),
                "$.productId"));
        }

        string? releaseId = ReadString(root, "releaseId");
        if (!string.IsNullOrWhiteSpace(request.ReleaseId)
            && releaseId is not null
            && !string.Equals(releaseId, request.ReleaseId, StringComparison.Ordinal))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                ReleaseMismatchCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"manifest releaseId '{releaseId}' does not match build ReleaseId '{request.ReleaseId}'."),
                "$.releaseId"));
        }
    }

    private static void ValidateSchemaCompatibility(
        JsonElement root,
        int supportedSchemaVersion,
        List<ManifestValidationDiagnostic> diagnostics)
    {
        if (root.TryGetProperty("schemaVersion", out JsonElement schemaVersion)
            && schemaVersion.ValueKind == JsonValueKind.Number
            && schemaVersion.TryGetInt32(out int manifestSchemaVersion)
            && manifestSchemaVersion > supportedSchemaVersion)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SchemaCompatibilityCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"manifest schemaVersion {manifestSchemaVersion} exceeds supported schema version {supportedSchemaVersion}."),
                "$.schemaVersion"));
        }

        if (root.TryGetProperty("compatibility", out JsonElement compatibility)
            && compatibility.ValueKind == JsonValueKind.Object
            && compatibility.TryGetProperty("minimumReaderSchemaVersion", out JsonElement minimumReaderSchemaVersion)
            && minimumReaderSchemaVersion.ValueKind == JsonValueKind.Number
            && minimumReaderSchemaVersion.TryGetInt32(out int minimumReader)
            && minimumReader > supportedSchemaVersion)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SchemaCompatibilityCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"manifest requires reader schema version {minimumReader}, but flagctl supports {supportedSchemaVersion}."),
                "$.compatibility.minimumReaderSchemaVersion"));
        }
    }

    private static void ValidateRules(JsonElement root, List<ManifestValidationDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("flags", out JsonElement flags) || flags.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int flagIndex = 0;
        foreach (JsonElement flag in flags.EnumerateArray())
        {
            if (flag.ValueKind != JsonValueKind.Object
                || !flag.TryGetProperty("rules", out JsonElement rules)
                || rules.ValueKind != JsonValueKind.Array)
            {
                flagIndex++;
                continue;
            }

            int ruleIndex = 0;
            foreach (JsonElement rule in rules.EnumerateArray())
            {
                string path = RulePath(flagIndex, ruleIndex);
                if (rule.ValueKind == JsonValueKind.Object
                    && rule.TryGetProperty("when", out JsonElement when)
                    && when.ValueKind == JsonValueKind.String)
                {
                    string expression = when.GetString() ?? string.Empty;
                    if (!CelSyntaxValidator.TryValidate(expression, out string message))
                    {
                        diagnostics.Add(new ManifestValidationDiagnostic(
                            CelSyntaxCode,
                            message,
                            string.Concat(path, ".when")));
                    }
                }

                ruleIndex++;
            }

            flagIndex++;
        }
    }

    private static void ValidateSignature(
        JsonElement root,
        string? publicKeyPath,
        List<ManifestValidationDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("signature", out JsonElement signature))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SignatureRequiredCode,
                "manifest must include a signature object for v1 bundled defaults.",
                "$.signature"));
            ValidatePublicKey(publicKeyPath, diagnostics);
            return;
        }

        if (signature.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SignatureObjectCode,
                "signature must be an object.",
                "$.signature"));
            ValidatePublicKey(publicKeyPath, diagnostics);
            return;
        }

        string? algorithm = ReadString(signature, "algorithm");
        if (!string.Equals(algorithm, "Ed25519", StringComparison.Ordinal))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SignatureAlgorithmCode,
                "signature.algorithm must be Ed25519.",
                "$.signature.algorithm"));
        }

        string? keyId = ReadString(signature, "keyId");
        if (string.IsNullOrWhiteSpace(keyId))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SignatureKeyIdCode,
                "signature.keyId must be a non-empty string.",
                "$.signature.keyId"));
        }

        string? value = ReadString(signature, "value");
        if (!TryDecodeExactLength(value, 64))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                SignatureValueCode,
                "signature.value must be a 64-byte base64 Ed25519 signature.",
                "$.signature.value"));
        }

        ValidatePublicKey(publicKeyPath, diagnostics);
    }

    private static void ValidatePublicKey(string? publicKeyPath, List<ManifestValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPath))
        {
            return;
        }

        byte[] keyBytes;
        try
        {
            keyBytes = File.ReadAllBytes(publicKeyPath);
        }
        catch (IOException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                PublicKeyReadCode,
                string.Concat("public key file could not be read: ", exception.Message),
                publicKeyPath));
            return;
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                PublicKeyReadCode,
                string.Concat("public key file could not be read: ", exception.Message),
                publicKeyPath));
            return;
        }

        if (keyBytes.Length == 32)
        {
            return;
        }

        string publicKeyText = File.ReadAllText(publicKeyPath).Trim();
        if (!TryDecodeExactLength(publicKeyText, 32))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                PublicKeyFormatCode,
                "public key must be raw 32-byte Ed25519 material or a base64-encoded 32-byte Ed25519 key.",
                publicKeyPath));
        }
    }

    private static void ValidateGeneratedBindings(
        ManifestValidationRequest request,
        IReadOnlyDictionary<string, FlagInfo> flags,
        List<ManifestValidationDiagnostic> diagnostics)
    {
        foreach (string bindingPath in request.GeneratedBindingPaths)
        {
            ValidateGeneratedBindingFile(bindingPath, request.ProductId, flags, diagnostics);
        }
    }

    private static void ValidateGeneratedBindingFile(
        string bindingPath,
        string? buildProductId,
        IReadOnlyDictionary<string, FlagInfo> flags,
        List<ManifestValidationDiagnostic> diagnostics)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(bindingPath), JsonOptions);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                BindingJsonInvalidCode,
                string.Concat("generated bindings JSON could not be parsed: ", exception.Message),
                bindingPath));
            return;
        }
        catch (IOException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                BindingSchemaCode,
                string.Concat("generated bindings file could not be read: ", exception.Message),
                bindingPath));
            return;
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                BindingSchemaCode,
                string.Concat("generated bindings file could not be read: ", exception.Message),
                bindingPath));
            return;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("bindings", out JsonElement bindings)
                || bindings.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(new ManifestValidationDiagnostic(
                    BindingSchemaCode,
                    "generated bindings file must contain a bindings array.",
                    bindingPath));
                return;
            }

            int index = 0;
            foreach (JsonElement binding in bindings.EnumerateArray())
            {
                ValidateGeneratedBinding(bindingPath, index, binding, buildProductId, flags, diagnostics);
                index++;
            }
        }
    }

    private static void ValidateGeneratedBinding(
        string bindingPath,
        int index,
        JsonElement binding,
        string? buildProductId,
        IReadOnlyDictionary<string, FlagInfo> flags,
        List<ManifestValidationDiagnostic> diagnostics)
    {
        string bindingRootPath = string.Create(CultureInfo.InvariantCulture, $"$.bindings[{index}]");
        if (binding.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                BindingSchemaCode,
                "generated binding entries must be objects.",
                string.Concat(bindingPath, ":", bindingRootPath)));
            return;
        }

        string? flagKey = ReadString(binding, "flagKey");
        if (string.IsNullOrWhiteSpace(flagKey) || !flags.TryGetValue(flagKey, out FlagInfo? flag))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                MissingFlagCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"generated binding references unknown flag key '{flagKey ?? string.Empty}'."),
                string.Concat(bindingPath, ":", bindingRootPath, ".flagKey")));
            return;
        }

        string? kind = ReadString(binding, "kind");
        string? valueType = ReadString(binding, "valueType");
        if (string.Equals(kind, "gate", StringComparison.Ordinal)
            && !string.Equals(flag.Type, "boolean", StringComparison.Ordinal))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                TypeMismatchCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"generated gate requires boolean flag '{flagKey}', but manifest type is '{flag.Type}'."),
                string.Concat(bindingPath, ":", bindingRootPath, ".kind")));
        }

        if (string.Equals(kind, "accessor", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(valueType)
            && TryMapClrType(valueType, out string expectedManifestType)
            && !string.Equals(flag.Type, expectedManifestType, StringComparison.Ordinal))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                TypeMismatchCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"generated accessor CLR type '{valueType}' does not match manifest type '{flag.Type}' for flag '{flagKey}'."),
                string.Concat(bindingPath, ":", bindingRootPath, ".valueType")));
        }

        string? productId = ReadString(binding, "productId") ?? buildProductId;
        if (!string.IsNullOrWhiteSpace(productId) && !flag.ProductScope.Contains(productId, StringComparer.Ordinal))
        {
            diagnostics.Add(new ManifestValidationDiagnostic(
                ProductScopeCode,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"generated binding productId '{productId}' is not in productScope for flag '{flagKey}'."),
                string.Concat(bindingPath, ":", bindingRootPath, ".productId")));
        }
    }

    private static Dictionary<string, FlagInfo> ReadFlags(JsonElement root)
    {
        Dictionary<string, FlagInfo> flags = new(StringComparer.Ordinal);
        if (!root.TryGetProperty("flags", out JsonElement flagsElement) || flagsElement.ValueKind != JsonValueKind.Array)
        {
            return flags;
        }

        foreach (JsonElement flag in flagsElement.EnumerateArray())
        {
            if (flag.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? key = ReadString(flag, "key");
            string? type = ReadString(flag, "type");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            List<string> productScope = [];
            if (flag.TryGetProperty("productScope", out JsonElement productScopeElement)
                && productScopeElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement product in productScopeElement.EnumerateArray())
                {
                    string? productId = product.ValueKind == JsonValueKind.String ? product.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(productId))
                    {
                        productScope.Add(productId);
                    }
                }
            }

            flags[key] = new FlagInfo(type, productScope);
        }

        return flags;
    }

    private static bool TryMapClrType(string valueType, out string manifestType)
    {
        manifestType = valueType switch
        {
            "bool" or "Boolean" or "System.Boolean" => "boolean",
            "string" or "String" or "System.String" => "string",
            "int" or "Int32" or "System.Int32" or "long" or "Int64" or "System.Int64" => "integer",
            "double" or "Double" or "System.Double" or "float" or "Single" or "System.Single" or "decimal" or "Decimal" or "System.Decimal" => "number",
            _ => string.Empty,
        };

        return manifestType.Length > 0;
    }

    private static bool TryDecodeExactLength(string? text, int length)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(text).Length == length;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private static string RulePath(int flagIndex, int ruleIndex) =>
        string.Create(CultureInfo.InvariantCulture, $"$.flags[{flagIndex}].rules[{ruleIndex}]");

    private static ManifestValidationSummary ToSummary(List<ManifestValidationDiagnostic> diagnostics) =>
        new(diagnostics.Count == 0, diagnostics);

    private sealed record FlagInfo(string Type, IReadOnlyCollection<string> ProductScope);
}
