# Flag Manifest Reference

A **manifest** is an immutable, signed JSON document that carries all feature flag definitions for a given product and release. The SDK evaluates flags against a manifest, never against a live database. Every shipped binary embeds a bundled default manifest; a remote override manifest can be fetched and cached at runtime.

---

## JSON Schema Overview

A manifest is a plain JSON object. Trailing commas and comments are not allowed. The document must be strict JSON (no relaxed parsing).

```
{
  "schemaVersion": <integer>,
  "productId":     <string>,
  "releaseId":     <string>,
  "environment":   <string>,
  "flags":         [ <flag>, ... ]
}
```

---

## Root Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `schemaVersion` | integer | yes | Must be `1`. Controls which features are available (e.g. CEL rules require `schemaVersion: 1`). |
| `productId` | string | yes | Identifies the product this manifest belongs to. Must be non-empty. The same value must appear in every flag's `productScope`. |
| `releaseId` | string | yes | Identifies the release cut. Combined with `productId` to uniquely address a manifest. Must be non-empty. |
| `environment` | string | yes | Deployment environment. Must be one of `Development`, `Staging`, or `Production` (case-sensitive). See note below. |
| `flags` | array | yes | Ordered list of flag definitions. May be empty. |

### Environment Values

Published manifests always carry exactly one of `Development`, `Staging`, or `Production`. Custom environment names are only used inside the admin plane during the draft authoring workflow; the admin normalizes them to one of these three canonical values before publishing a manifest. A manifest that reaches the SDK or CI validator will never contain a custom environment name.

---

## Flag Object Fields

Each entry in `flags` is a JSON object with the following fields.

| Field | Type | Required | Description |
|---|---|---|---|
| `key` | string | yes | Unique, non-empty flag identifier within this manifest. Dot-separated naming is conventional (e.g. `search.enabled`). |
| `type` | string | yes | Value type. Must be one of `boolean`, `string`, `integer`, or `number`. |
| `defaultValue` | any | yes | Value returned when no rule matches. Must match the declared `type`. |
| `killable` | boolean | yes | When `true`, the flag supports a forced-refresh path that bypasses normal cache TTLs. |
| `productScope` | array of strings | yes | Products that may evaluate this flag. Must include the root `productId`. Flags evaluated from a product not listed here return the caller's default value and emit a warning. |
| `rules` | array | no | Ordered list of conditional overrides. Omit the field entirely if no rules are needed. |

### Flag Types

| Type | JSON representation | Example `defaultValue` |
|---|---|---|
| `boolean` | JSON `true` / `false` | `true` |
| `string` | JSON string | `"classic"` |
| `integer` | JSON number with no decimal part | `10` |
| `number` | Any JSON number | `0.75` |

The validator checks that `defaultValue` and every rule's `value` match the declared `type`. For `integer`, the value must be representable as a 64-bit signed integer.

---

## Rules Array

The `rules` field is optional. When present it must be a JSON array. Each element is evaluated in order; the first matching rule wins and its `value` is returned. If no rule matches, `defaultValue` is returned.

### Rule Object Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `when` | string | yes | A CEL boolean expression. See [CEL Rules Reference](CEL-Rules-Reference.md). |
| `value` | any | yes | The flag value returned when `when` evaluates to `true`. Must match the flag's `type`. |

Rules require `schemaVersion: 1`. Using `rules` in a manifest with any other `schemaVersion` value is a validation error.

---

## Signing: SignedManifestEnvelope

Manifests are not distributed as raw JSON. The distribution layer wraps the canonical JSON payload in a `SignedManifestEnvelope` before storing or transmitting it. The SDK verifies the signature before accepting any manifest.

| Envelope Field | Type | Description |
|---|---|---|
| `ManifestJson` | string | The canonical manifest JSON payload. |
| `Signature` | string | Signature over the manifest JSON. |
| `SigningKeyId` | string | Identifier for the signing key. Used for key rotation. |
| `Algorithm` | string | Signature algorithm name (e.g. `Ed25519`). |
| `ManifestId` | string | SHA-256 hex digest of the manifest JSON. Derived automatically; not written by the author. |
| `ETag` | string? | Optional HTTP ETag for conditional refresh requests. |
| `PublishedAt` | DateTimeOffset? | Optional publication timestamp. |

Manifests are signed with **Ed25519**. The public key is embedded in the SDK at build time. Key rotation requires a new SDK build. The SDK discards any manifest whose signature does not verify against the embedded public key.

---

## Complete Annotated Example

```json
{
  "schemaVersion": 1,
  "productId": "truckmate",
  "releaseId": "2026.05",
  "environment": "Production",
  "flags": [
    {
      "key": "new-dashboard",
      "type": "boolean",
      "defaultValue": true,
      "killable": true,
      "productScope": [ "truckmate" ]
    },
    {
      "key": "theme",
      "type": "string",
      "defaultValue": "classic",
      "killable": false,
      "productScope": [ "truckmate", "dispatch" ],
      "rules": [
        {
          "when": "project == 'alpha'",
          "value": "modern"
        },
        {
          "when": "user.region == 'us'",
          "value": "regional"
        }
      ]
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
    },
    {
      "key": "rollout",
      "type": "string",
      "defaultValue": "off",
      "killable": false,
      "productScope": [ "truckmate" ],
      "rules": [
        {
          "when": "bucket(UserId, 50)",
          "value": "on"
        }
      ]
    }
  ]
}
```

Annotation notes:

- `new-dashboard` is a simple boolean flag with no rules. `killable: true` means it can be force-refreshed during an incident.
- `theme` is shared across two products (`truckmate` and `dispatch`) and has two ordered rules. The first matching rule wins.
- `search.limit` is an integer flag. Its `defaultValue` must be a whole number.
- `search.weight` is a floating-point number flag.
- `rollout` uses `bucket(UserId, 50)` to deterministically assign 50% of users to the `"on"` value based on their `UserId` context key.

---

## What `flagctl validate` Checks

The CLI validator (`flagctl validate`) runs `ManifestValidator` against the manifest file and reports all errors before any binary is built. Validation is designed to be run in CI and fails the build on any error.

Checks performed (in order):

1. **JSON parse** - The document must be valid strict JSON. Trailing commas and comments are rejected (`FFMANIFEST_JSON_INVALID`).
2. **Root object** - The root value must be a JSON object (`FFMANIFEST_ROOT_OBJECT`).
3. **Required root fields** - `schemaVersion`, `productId`, `releaseId`, `environment`, and `flags` must all be present (`FFMANIFEST_REQUIRED_FIELD`).
4. **schemaVersion** - Must be the integer `1` (`FFMANIFEST_SCHEMA_VERSION`).
5. **productId / releaseId** - Must be non-empty strings (`FFMANIFEST_STRING_REQUIRED`).
6. **environment** - Must be exactly `Development`, `Staging`, or `Production` (`FFMANIFEST_ENVIRONMENT`).
7. **flags** - Must be a JSON array (`FFMANIFEST_FLAGS_ARRAY`).
8. **Flag objects** - Each array element must be an object (`FFMANIFEST_FLAG_OBJECT`).
9. **Duplicate keys** - No two flags may share the same `key` value (`FFMANIFEST_DUPLICATE_KEY`).
10. **Flag type** - `type` must be one of `boolean`, `string`, `integer`, or `number` (`FFMANIFEST_FLAG_TYPE`).
11. **defaultValue type** - `defaultValue` must match the declared `type` (`FFMANIFEST_DEFAULT_VALUE_TYPE`).
12. **killable** - Must be a boolean (`FFMANIFEST_BOOLEAN_REQUIRED`).
13. **productScope** - Must be a non-empty array of non-empty strings that includes the manifest's `productId` (`FFMANIFEST_PRODUCT_SCOPE_ARRAY`, `FFMANIFEST_PRODUCT_SCOPE_EMPTY`, `FFMANIFEST_PRODUCT_SCOPE_ITEM`, `FFMANIFEST_PRODUCT_SCOPE_PRODUCT`).
14. **rules** - When present, must be an array; requires `schemaVersion: 1` (`FFMANIFEST_RULES_ARRAY`, `FFMANIFEST_RULE_SCHEMA_VERSION`).
15. **Rule objects** - Each rule must be a JSON object with `when` and `value` (`FFMANIFEST_RULE_OBJECT`).
16. **Rule value type** - `value` must match the flag's declared `type` (`FFMANIFEST_RULE_VALUE_TYPE`).
17. **Rule when syntax** - `when` must be a valid CEL expression (`FFMANIFEST_RULE_WHEN_SYNTAX`).
18. **Rule when type** - The CEL expression in `when` must evaluate to a boolean (`FFMANIFEST_RULE_WHEN_TYPE`).

All errors are reported in a single pass; the validator does not stop at the first error. Each error includes a stable machine-readable code, a human-readable message, and a JSON path (e.g. `$.flags[2].rules[0].value`).
