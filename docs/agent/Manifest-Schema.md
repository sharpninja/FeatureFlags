# Manifest Schema

A feature flag manifest is a JSON document describing flags, rules, and signing metadata for one `(productId, releaseId, environment)` tuple.

## Root Object

```json
{
  "schemaVersion": 1,
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "production",
  "flags": [ ... ],
  "signature": { ... },
  "compatibility": { ... }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `schemaVersion` | `integer` | Yes | Must be `1` in v1. The SDK rejects manifests where this exceeds the reader's supported version. |
| `productId` | `string` | Yes | Product identifier. V1 catalog: `truckmate`, `drivermate`. |
| `releaseId` | `string` | Yes | Release identifier. Must match the SDK's stamped `ReleaseId`. |
| `environment` | `string` | Yes | One of `development`, `staging`, `production`. |
| `flags` | `array` | Yes | Array of flag objects. May be empty. |
| `signature` | `object` | Yes | Ed25519 signature envelope. |
| `compatibility` | `object` | No | Reader compatibility constraints. |

### environment constraint

Published manifests accept exactly three values: `development`, `staging`, `production`. The Admin plane may accept custom names during draft workflows but normalizes them before producing a publishable manifest. `flagctl validate` enforces the three-value restriction.

---

## Flag Object

```json
{
  "key": "dashboard.enabled",
  "type": "boolean",
  "defaultValue": false,
  "killable": true,
  "productScope": ["truckmate"],
  "rules": [ ... ]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `key` | `string` | Yes | Flag identifier. Unique within the manifest. Dot notation recommended (e.g., `feature.sub-feature`). |
| `type` | `string` | Yes | Flag value type. One of `boolean`, `string`, `integer`, `number`. |
| `defaultValue` | matches `type` | Yes | Fallback value when no rule matches and the flag is not disabled. |
| `killable` | `boolean` | No | When `true`, the kill switch can disable this flag entirely. Default: `false`. |
| `productScope` | `array of string` | Yes | Products for which this flag is active. Each entry must be a valid product ID. |
| `rules` | `array` | Yes | Ordered rule list. May be empty. |

### type values

| Manifest type | JSON type | C# type | Notes |
|---|---|---|---|
| `boolean` | `true` / `false` | `bool` | |
| `string` | `"..."` | `string` | |
| `integer` | `0` | `int`, `long` | Must be a JSON integer (no decimal point). |
| `number` | `0.0` | `double`, `float`, `decimal` | Accepts integers or decimals. |

---

## Rule Object

```json
{
  "when": "context.user.role == 'admin'",
  "value": true
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `when` | `string` | Yes | CEL expression. Must evaluate to `boolean`. First matching rule wins. |
| `value` | matches flag `type` | Yes | Value to return when `when` is `true`. |

Rules are evaluated in order. The first rule whose `when` expression returns `true` determines the flag value. If no rule matches, `defaultValue` is returned.

See [CEL Reference](CEL-Reference.md) for the full expression language.

---

## Signature Object

```json
{
  "signature": {
    "algorithm": "Ed25519",
    "keyId": "truckmate-prod-key-2026",
    "value": "<base64-encoded 64-byte Ed25519 signature>"
  }
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `algorithm` | `string` | Yes | Must be `Ed25519`. `flagctl validate` rejects any other value. |
| `keyId` | `string` | Yes | Non-empty identifier for the signing key. Used for key rotation tracking. |
| `value` | `string` | Yes | Base64-encoded 64-byte Ed25519 signature over the manifest content (excluding the signature object itself). |

### Development shortcut

When calling `AddSharpNinjaFeatureFlags(options, rawJson)` directly, the SDK accepts a structural signature:

```json
{
  "signature": {
    "algorithm": "structural",
    "keyId": "bundled-development-key",
    "value": "bundled-development-signature"
  }
}
```

This path skips cryptographic verification. It is for development and unit testing only. `flagctl validate` rejects structural signatures.

### Public key format

The public key file (`flags/public-key.ed25519`) contains either:
- Raw 32-byte binary Ed25519 material, or
- Base64-encoded 32-byte Ed25519 material (text file)

`flagctl validate --public-key <path>` validates the key format.

---

## Compatibility Object

```json
{
  "compatibility": {
    "minimumReaderSchemaVersion": 1
  }
}
```

| Field | Type | Description |
|---|---|---|
| `minimumReaderSchemaVersion` | `integer` | Minimum schema version a reader must support to parse this manifest. The SDK and `flagctl` reject manifests where this exceeds the supported version. |

---

## Complete Example

```json
{
  "schemaVersion": 1,
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "production",
  "flags": [
    {
      "key": "dashboard.enabled",
      "type": "boolean",
      "defaultValue": false,
      "killable": true,
      "productScope": ["truckmate"],
      "rules": [
        {
          "when": "context.user.role == 'admin'",
          "value": true
        },
        {
          "when": "bucket(context.user.id, 0.2)",
          "value": true
        }
      ]
    },
    {
      "key": "reports.title",
      "type": "string",
      "defaultValue": "Reports",
      "killable": false,
      "productScope": ["truckmate"],
      "rules": [
        {
          "when": "context.tenant.id == 'acme'",
          "value": "ACME Reports"
        }
      ]
    },
    {
      "key": "max.concurrent.uploads",
      "type": "integer",
      "defaultValue": 5,
      "killable": false,
      "productScope": ["truckmate", "drivermate"],
      "rules": [
        {
          "when": "context.env.tier == 'premium'",
          "value": 50
        }
      ]
    }
  ],
  "signature": {
    "algorithm": "Ed25519",
    "keyId": "truckmate-prod-2026-05",
    "value": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
  }
}
```

---

## Validation Checks

`flagctl validate` runs these checks in order. Each check produces a diagnostic code on failure:

| Code | Check |
|---|---|
| `FFMANIFEST_FILE_READ` | Manifest file exists and is readable. |
| (JSON parse) | File is valid JSON (no trailing commas, no comments). |
| `FFMANIFEST_PRODUCT_MISMATCH` | `$.productId` matches `--product-id` when supplied. |
| `FFMANIFEST_RELEASE_MISMATCH` | `$.releaseId` matches `--release-id` when supplied. |
| `FFMANIFEST_SCHEMA_COMPATIBILITY` | `$.schemaVersion` and `$.compatibility.minimumReaderSchemaVersion` do not exceed supported version. |
| `FFMANIFEST_CEL_SYNTAX` | Every `$.flags[*].rules[*].when` expression parses without syntax error. |
| `FFMANIFEST_SIGNATURE_REQUIRED` | `$.signature` object is present. |
| `FFMANIFEST_SIGNATURE_OBJECT` | `$.signature` is a JSON object. |
| `FFMANIFEST_SIGNATURE_ALGORITHM` | `$.signature.algorithm` is `Ed25519`. |
| `FFMANIFEST_SIGNATURE_KEYID` | `$.signature.keyId` is a non-empty string. |
| `FFMANIFEST_SIGNATURE_VALUE` | `$.signature.value` is a valid base64-encoded 64-byte string. |
| `FFMANIFEST_PUBLIC_KEY_READ` | Public key file is readable (when `--public-key` supplied). |
| `FFMANIFEST_PUBLIC_KEY_FORMAT` | Public key is 32-byte binary or base64-encoded 32-byte material. |
| `SNFF_BINDING_JSON_INVALID` | Generated binding JSON parses without error (when `--generated-bindings` supplied). |
| `SNFF_BINDING_SCHEMA` | Binding file has a `bindings` array. |
| `SNFF0001` | Every binding's `flagKey` exists in the manifest. |
| `SNFF0002` | Binding CLR type matches manifest flag type. |
| `SNFF0003` | Binding `productId` is in the flag's `productScope`. |

---

## JSON Constraints

- No trailing commas.
- No comments (JSON comments not allowed).
- String values are UTF-8.
- Numeric flag `defaultValue` and rule `value` must be the correct JSON number type (integer for `integer` flags, number for `number` flags).
- The `flags` array may be empty but must be present.
