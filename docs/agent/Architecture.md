# Architecture

## System Overview

```
+--------------------------------------------------+
|                  Application Binary               |
|                                                  |
|  +--------------------------------------------+ |
|  |  SharpNinja.FeatureFlags (SDK)             | |
|  |                                            | |
|  |  ISharpNinjaFeatureClient                  | |
|  |    .Evaluate<T>(key, default, context)     | |
|  |                                            | |
|  |  Three-tier cache (read order):            | |
|  |    1. In-memory (latest verified manifest) | |
|  |    2. On-disk  (%LOCALAPPDATA%/...)        | |
|  |    3. Bundled default (embedded resource)  | |
|  +--------------------------------------------+ |
|         |                      ^                 |
|         | background           | manifest        |
|         v                      | (signed JSON)   |
|  RemoteFetchCoordinator   ExposureUpload         |
+--------------------------------------------------+
         |                      |
         | GET /v1/manifest/    | POST /v1/exposure
         v                      v
+--------------------------------------------------+
|       Distribution Service  (port 18081)         |
|                                                  |
|  - Delivers signed manifests (ETag, CDN headers) |
|  - Accepts exposure event batches               |
|  - Auth: X-SharpNinja-Api-Key (per-product)     |
|  - Storage: FileSystem | InMemory               |
|  - Metrics: Prometheus /metrics                 |
+--------------------------------------------------+
         ^
         | (operator loads manifests)
         |
+--------------------------------------------------+
|         Admin Service  (port 18080)              |
|                                                  |
|  IAdminRuntimeService:                          |
|    CreateDraftAsync  → draft (revision 1)       |
|    UpdateDraftAsync  → draft (revision N+1)     |
|    PublishAsync      → audit entry              |
|    PromoteAsync      → draft in target env      |
|                                                  |
|  Audit trail: append-only, immutable            |
|  Auth: OIDC or test-mode headers               |
|  RBAC: Viewer/Editor/Publisher/Promoter/KeyAdmin|
+--------------------------------------------------+
         ^
         | MSBuild build-time
         |
+--------------------------------------------------+
|         Build Tooling (SharpNinja.FeatureFlags.Build)  |
|                                                  |
|  Targets (before CoreCompile):                  |
|    1. ValidateSharpNinjaFeatureFlagBuildProperties  |
|    2. GenerateSharpNinjaFeatureFlagsRegistrationSource  |
|    3. ValidateSharpNinjaFeatureFlagManifest     |
|                                                  |
|  Embeds: flags.json + public-key.ed25519        |
|  Stamps: AssemblyMetadataAttribute (ProductId,  |
|          ReleaseId, resource names)             |
|  Invokes: flagctl validate                      |
+--------------------------------------------------+
```

## Components

### SDK (`SharpNinja.FeatureFlags`)

The runtime evaluation library embedded in every consumer application.

**Responsibilities:**
- Load, verify, and cache the signed manifest
- Evaluate flag values synchronously against CEL rules
- Record exposure events to an on-disk outbox
- Background-refresh the manifest from the Distribution service
- Background-upload exposure events to the Distribution service

**Key types:**
- `ISharpNinjaFeatureClient` - public evaluation surface
- `SharpNinjaFeatureFlagOptions` - configuration record (required at registration)
- `EvaluationContext` - per-call evaluation attributes
- `EvaluationResult<T>` - evaluation output with value and reason

**Guarantee:** `Evaluate<T>()` is always synchronous and never touches the network. Network failures cannot cause evaluation latency or exceptions.

### Distribution Service

A standalone HTTP service (ASP.NET Core) that distributes manifests to SDK instances.

**Responsibilities:**
- Serve signed manifests addressed by `(productId, releaseId, environment)`
- Respond with HTTP 304 when the client's ETag matches (CDN-friendly)
- Accept and persist exposure event batches from SDK instances
- Expose Prometheus metrics at `/metrics`

**Auth:** Per-product API keys validated from configuration. Optionally requires device attestation.

**Storage:** FileSystem (default) or InMemory (test). Manifests are loaded by operators from the Admin service output.

### Admin Service

A standalone HTTP service (ASP.NET Core) for flag authoring and governance.

**Responsibilities:**
- Manage feature flag drafts (CRUD per product/environment)
- Sign and publish drafts to the Distribution service
- Track every state change in an append-only audit trail
- Enforce RBAC for all operations

**Auth:** OIDC (production) or test-mode request headers. Claims provide tenant, product grants, and role grants.

### Build Tooling (`SharpNinja.FeatureFlags.Build`)

A NuGet package that delivers MSBuild targets via `buildTransitive`. No explicit `<Import>` needed.

**Responsibilities:**
- Validate required MSBuild properties (`ProductId`, `ReleaseId`)
- Embed `flags.json` and `public-key.ed25519` as assembly resources
- Stamp `AssemblyMetadataAttribute` entries with product identity
- Invoke `flagctl validate` before `CoreCompile` to catch manifest errors early
- Optionally generate a zero-argument `AddSharpNinjaFeatureFlags()` overload

### CEL Evaluator (`SharpNinja.FeatureFlags.Evaluation`)

A tree-walking Common Expression Language interpreter. AOT-safe (no `Reflection.Emit`).

**Supported:** Comparison, logical, arithmetic, membership (`in`), ternary (`?:`), member access (dot and index), map and list literals, macros (`exists`, `all`, `filter`, `map`, `exists_one`), custom functions (`semver_compare`, `semver_satisfies`, `bucket`).

**Constraint:** Macro iteration cap is 512 elements. Expressions exceeding this cap evaluate to `false`.

### Manifest Layer (`SharpNinja.FeatureFlags.Manifest`)

**Responsibilities:**
- Parse and validate manifest JSON
- Verify Ed25519 signatures
- Enforce schema version compatibility

## Data Flow: Flag Evaluation

```
1. App starts
   -> SDK reads bundled resource (flags.json embedded by Build target)
   -> Verifies Ed25519 signature
   -> Loads into in-memory cache

2. Background fetch (interval: ManifestRefreshInterval, default 5 min)
   -> GET /v1/manifest/{productId}/{releaseId}?environment={env}
   -> Distribution returns signed JSON + ETag
   -> SDK verifies signature
   -> Updates in-memory cache + writes to disk cache

3. Flag evaluation (every call)
   -> Read in-memory manifest (lock-free)
   -> Run CEL evaluator against flag rules + EvaluationContext
   -> Return EvaluationResult<T> with value and reason
   -> Append ExposureEvent to outbox (async, non-blocking)

4. Exposure upload (interval: ExposureUploadInterval, default 30 sec)
   -> Drain on-disk outbox
   -> POST /v1/exposure to Distribution service
   -> Distribution persists events
```

## Data Flow: Flag Authoring

```
1. Author creates draft
   -> POST Admin -> CreateDraftAsync(mutation)
   -> Returns FeatureFlagDraft (revision 1)
   -> Audit entry: Action=Created

2. Author refines draft
   -> POST Admin -> UpdateDraftAsync(mutation)
   -> Returns FeatureFlagDraft (revision N+1)
   -> Audit entry: Action=Updated

3. Publisher publishes draft
   -> POST Admin -> PublishAsync(action)
   -> Returns AdminAuditEntry
   -> Audit entry: Action=Published
   -> Operator copies signed manifest to Distribution service storage

4. Promoter promotes to next environment
   -> POST Admin -> PromoteAsync(action)
   -> Returns FeatureFlagDraft in target environment
   -> Audit entry: Action=Promoted, TargetEnvironmentName set
```

## Identity Model

Every manifest is addressed by a three-part key:

```
(productId, releaseId, environment)
```

- `productId`: enumerated catalog value (`truckmate`, `drivermate` in v1)
- `releaseId`: free-form release identifier (e.g., `truckmate-1.2.0-stable-0`)
- `environment`: one of `development`, `staging`, `production`

The SDK validates this triple at startup. A mismatch between the MSBuild-stamped identity and the manifest throws at registration time.

## Signing Model

Manifests are signed with Ed25519. The signature envelope is embedded in the manifest JSON:

```json
{
  "signature": {
    "algorithm": "Ed25519",
    "keyId": "<key-identifier>",
    "value": "<64-byte base64 signature>"
  }
}
```

The public key is embedded in the application binary as a resource. The SDK verifies the signature on every manifest load (bundled, disk cache, and remote fetch). Manifests with invalid or missing signatures are rejected.

**Development shortcut:** When calling `AddSharpNinjaFeatureFlags(options, rawJson)` directly (not from an embedded resource), the SDK accepts the structural signature `{"algorithm":"structural","keyId":"bundled-development-key","value":"bundled-development-signature"}`. This path is for development only; production must use a real Ed25519 signature.

## Bucketing Hash

Percentage bucketing uses FNV-1a 64-bit (not the SipHash-2-4 recommended by TR-3). See `docs/adr/ADR-001-fnv1a-bucketing-hash.md` for the decision record. The constants are pinned by a determinism test: offset basis `14695981039346656037`, prime `1099511628211`.
