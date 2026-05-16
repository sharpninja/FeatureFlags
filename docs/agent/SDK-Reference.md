# SDK Reference

The SDK (`SharpNinja.FeatureFlags`) evaluates feature flags in the application process. All evaluation is synchronous and offline-first.

## Registration

### Required packages

```xml
<PackageReference Include="SharpNinja.FeatureFlags" Version="1.0.0" />
<!-- Optional: MSBuild embed + source gen -->
<PackageReference Include="SharpNinja.FeatureFlags.Build" Version="1.0.0" />
```

### Explicit registration

```csharp
var options = new SharpNinjaFeatureFlagOptions(
    productId: "truckmate",
    releaseId: "truckmate-1.2.0-stable-0",
    environment: "production",
    manifestRefreshInterval: TimeSpan.FromMinutes(5),
    exposureUploadInterval: TimeSpan.FromSeconds(30));

services.AddSharpNinjaFeatureFlags(options.Validate(), File.ReadAllText("flags/flags.json"));
```

### Generated registration (when Build target enabled)

```csharp
// Generated zero-argument overload (requires SharpNinjaFeatureFlagsGenerateRegistrationSource=true)
services.AddSharpNinjaFeatureFlags();
```

### Overloads

```csharp
// Overload 1: raw manifest JSON (development path; uses structural signature internally)
IServiceCollection AddSharpNinjaFeatureFlags(
    this IServiceCollection services,
    SharpNinjaFeatureFlagOptions options,
    string manifestJson);

// Overload 2: pre-signed envelope (production path)
IServiceCollection AddSharpNinjaFeatureFlags(
    this IServiceCollection services,
    SharpNinjaFeatureFlagOptions options,
    SignedManifestEnvelope manifestEnvelope);
```

---

## SharpNinjaFeatureFlagOptions

Namespace: `SharpNinja.FeatureFlags.Abstractions.Options`

### Constructor parameters (all required)

| Parameter | Type | Description |
|---|---|---|
| `ProductId` | `string` | Product identifier. Must be in the supported catalog (`truckmate`, `drivermate`). |
| `ReleaseId` | `string` | Release identifier. Must match the manifest's `releaseId`. |
| `Environment` | `string` | Environment name. Must match the manifest's `environment`. |
| `ManifestRefreshInterval` | `TimeSpan` | How often to poll the Distribution service. Must be positive. Recommended: 5 min. |
| `ExposureUploadInterval` | `TimeSpan` | How often to drain the exposure outbox. Must be positive. Recommended: 30 sec. |

### Init-only properties (optional)

| Property | Type | Default | Description |
|---|---|---|---|
| `DistributionBaseUri` | `Uri?` | `null` | Distribution service base URI. Required for remote manifest refresh. |
| `ExposureUploadEndpoint` | `Uri?` | `null` | Explicit exposure upload URI. Overrides the endpoint derived from `DistributionBaseUri`. |
| `ManifestCachePath` | `string?` | `null` | On-disk manifest cache path. When null: `%LOCALAPPDATA%\SharpNinja\FeatureFlags\{productId}\{releaseId}\{environment}\manifest-cache.json`. |
| `ExposureOutboxPath` | `string?` | `null` | On-disk exposure outbox path. When null: platform default under local app data. |
| `ExposureUploadBatchSize` | `int` | `100` | Max events per upload request. Must be positive. |
| `SupportedProductIds` | `IReadOnlyCollection<string>` | `["truckmate","drivermate"]` | Allowlist of valid product IDs. `ProductId` must appear here. |
| `AllowCustomEnvironments` | `bool` | `true` | When `false`, only `development`, `staging`, `production` are accepted. |
| `ExposureRetention` | `SharpNinjaExposureRetentionOptions` | 90-day | Retention policy for the on-disk exposure outbox. |
| `MultiTenant` | `SharpNinjaMultiTenantOptions` | `SingleTenant` | Tenant isolation mode. |

### Validation

Call `options.Validate()` before passing to `AddSharpNinjaFeatureFlags`. The method throws `InvalidOperationException` with a descriptive message on any constraint violation. The extension method calls `Validate()` internally.

### Default instance

`SharpNinjaFeatureFlagOptions.Default` targets `truckmate`, `truckmate-0.0.0-stable-0`, `development`, 5-minute refresh, 30-second upload. For development and testing only.

---

## ISharpNinjaFeatureClient

Namespace: `SharpNinja.FeatureFlags.Abstractions`

Resolve from the DI container:

```csharp
ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();
```

### Evaluate\<T\>

```csharp
EvaluationResult<T> Evaluate<T>(
    string key,
    T defaultValue,
    EvaluationContext? context = null);
```

Evaluates a flag synchronously. Never blocks on network I/O. Always returns a value; never throws for flag-not-found (returns `defaultValue` with `Reason = Default`).

**Type parameter `T`:** Must match the manifest flag type:

| Manifest type | C# type |
|---|---|
| `boolean` | `bool` |
| `string` | `string` |
| `integer` | `int`, `long` |
| `number` | `double`, `float`, `decimal` |

### EvaluateAsync\<T\>

```csharp
ValueTask<EvaluationResult<T>> EvaluateAsync<T>(
    string key,
    T defaultValue,
    EvaluationContext? context = null,
    CancellationToken cancellationToken = default);
```

Async-compatible surface. Resolves synchronously in v1; signature supports future network-aware evaluation.

---

## EvaluationContext

Namespace: `SharpNinja.FeatureFlags.Abstractions`

Carries per-call attributes used by CEL rule expressions.

```csharp
EvaluationContext context = EvaluationContext.Builder()
    .Set("user.role", "admin")
    .Set("tenant.id", "acme")
    .Set("app.version", "1.2.0")
    .Build();
```

### Well-known keys (`SharpNinjaEvaluationContextKeys`)

| Key | Typical value | Description |
|---|---|---|
| `ProductId` | `"truckmate"` | Compile-time product identity. |
| `ReleaseId` | `"truckmate-1.2.0-stable-0"` | Compile-time release identity. |
| `SemanticVersion` | `"1.2.0"` | Semantic version string. |
| `ReleaseChannel` | `"stable"` | Release channel. |
| `ReleaseBuild` | `"0"` | Build ordinal within channel. |
| `Environment` | `"production"` | Deployment environment. |
| `TenantId` | `"acme"` | Tenant identity (multi-tenant). |

Custom keys are allowed. Use `string` keys with any JSON-compatible value type (`bool`, `string`, `int`, `double`, `null`).

### Key naming convention

Well-known keys use dot notation. User-defined keys should follow the same convention: `category.attribute`. CEL expressions reference them as `context.category.attribute` or via the `context["key"]` index form.

---

## EvaluationResult\<T\>

| Property | Type | Description |
|---|---|---|
| `Value` | `T` | Resolved flag value. |
| `Reason` | `EvaluationReason` | Why this value was chosen. |
| `Variant` | `string?` | Matched variant identifier (if any). |
| `RuleIndex` | `int?` | Zero-based index of the matched rule (if `Reason` is `RuleMatch`). |
| `ErrorMessage` | `string?` | Error description when `Reason` is `Error`. |

### EvaluationReason values

| Value | Meaning |
|---|---|
| `Default` | No rule matched; default value returned. |
| `RuleMatch` | A manifest rule's `when` expression matched. |
| `TargetingMatch` | A targeting rule matched (product/environment/tenant scope). |
| `Disabled` | The kill switch fired; flag is disabled. |
| `Error` | Evaluation failed (CEL runtime error, type mismatch); fallback to default. |
| `Unknown` | Evaluator did not supply a specific reason. |

---

## Startup Behavior

At `AddSharpNinjaFeatureFlags` call time:

1. The manifest JSON is parsed and validated against the schema.
2. If a `SignedManifestEnvelope` overload is used, the Ed25519 signature is verified using the embedded public key resource.
3. `ProductId`, `ReleaseId`, and `Environment` in the manifest must match the options values. Mismatch throws `InvalidOperationException` immediately.
4. The manifest is loaded into the in-memory cache.

After `BuildServiceProvider()` and during the hosted service lifetime:

5. `SharpNinjaDiskManifestCacheStore` checks the platform disk cache. If a valid cached manifest exists with a good signature, it replaces the bundled manifest.
6. `SharpNinjaRemoteFetchCoordinator` starts polling `DistributionBaseUri` on `ManifestRefreshInterval` (if configured).
7. `SharpNinjaExposureUploadCoordinator` starts draining the exposure outbox on `ExposureUploadInterval` (if `DistributionBaseUri` or `ExposureUploadEndpoint` is configured).

---

## Disk Cache Layout

On-disk manifest cache default path:

```
%LOCALAPPDATA%\SharpNinja\FeatureFlags\{productId}\{releaseId}\{environment}\manifest-cache.json
```

The cached file is the full signed manifest JSON. The SDK verifies its signature before using it.

---

## Exposure Event Outbox

The SDK writes one `SharpNinjaExposureEvent` per `Evaluate` call to an on-disk outbox. The upload coordinator drains and POSTs batches to `/v1/exposure`.

Events are keyed by `(productId, releaseId, environment, flagKey, contextFingerprint)`. Duplicate events within a flush window are coalesced to reduce upload volume.

---

## Thread Safety

`ISharpNinjaFeatureClient` is safe for concurrent use from multiple threads. The in-memory manifest is accessed via lock-free reads. Background coordinators write to the manifest slot under a lock; evaluation is not affected during swaps.
