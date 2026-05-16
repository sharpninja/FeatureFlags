# Configuration Reference

This page documents every configuration type exposed by the SharpNinja Feature Flags SDK v1.0.0. All types live in the `SharpNinja.FeatureFlags.Abstractions.Options` namespace.

## SharpNinjaFeatureFlagOptions

The top-level options record. Pass an instance of this type to `AddSharpNinjaFeatureFlags`.

```csharp
public sealed record SharpNinjaFeatureFlagOptions(
    string ProductId,
    string ReleaseId,
    string Environment,
    TimeSpan ManifestRefreshInterval,
    TimeSpan ExposureUploadInterval);
```

### Constructor parameters (required)

| Parameter | Type | Description |
|---|---|---|
| `ProductId` | `string` | Compile-time product identifier. Must appear in `SupportedProductIds`. V1 catalog values: `"truckmate"`, `"drivermate"`. |
| `ReleaseId` | `string` | Compile-time release identifier stamped into the binary. Must match the manifest's `releaseId` field. |
| `Environment` | `string` | Target environment name. Must match the manifest's `environment` field. Built-in values: `"development"`, `"staging"`, `"production"`. |
| `ManifestRefreshInterval` | `TimeSpan` | How often the background coordinator polls the distribution service for a new manifest. Must be greater than zero. Default recommendation: `TimeSpan.FromMinutes(5)`. |
| `ExposureUploadInterval` | `TimeSpan` | How often the exposure outbox is drained and uploaded. Must be greater than zero. Default recommendation: `TimeSpan.FromSeconds(30)`. |

### Init-only properties (optional)

| Property | Type | Default | Description |
|---|---|---|---|
| `ReleaseLineage` | `SharpNinjaReleaseLineage` | Derived from `ReleaseId` | Full semver + channel + build lineage. See [SharpNinjaReleaseLineage](#sharpninjareleaselineage). |
| `DeploymentEnvironment` | `SharpNinjaDeploymentEnvironment` | Derived from `Environment` | Typed environment wrapper. See [SharpNinjaDeploymentEnvironment](#sharpninjadeploymentenvironment). |
| `AllowCustomEnvironments` | `bool` | `true` | When `false`, only `"development"`, `"staging"`, and `"production"` are accepted. |
| `ExposureRetention` | `SharpNinjaExposureRetentionOptions` | 90-day retention | Retention policy for the on-disk exposure outbox. See [SharpNinjaExposureRetentionOptions](#sharpninjaexposureretentionoptions). |
| `MultiTenant` | `SharpNinjaMultiTenantOptions` | `SingleTenant` | Tenant isolation settings. See [SharpNinjaMultiTenantOptions](#sharpninjamultitenantOptions). |
| `SupportedProductIds` | `IReadOnlyCollection<string>` | `["truckmate", "drivermate"]` | Allowlist of product identifiers. `ProductId` must appear in this list. |
| `ManifestCachePath` | `string?` | `null` | Explicit path for the on-disk manifest cache file. When `null`, the SDK uses `%LOCALAPPDATA%\SharpNinja\FeatureFlags\{productId}\{releaseId}\{environment}\manifest-cache.json`. |
| `ExposureOutboxPath` | `string?` | `null` | Explicit path for the on-disk exposure event outbox. When `null`, the SDK uses a platform default under the local application data folder. |
| `DistributionBaseUri` | `Uri?` | `null` | Base URI of the SharpNinja Distribution service. Required for remote manifest refresh. Must be an absolute URI when set. |
| `ExposureUploadEndpoint` | `Uri?` | `null` | Explicit exposure upload endpoint. Overrides the endpoint derived from `DistributionBaseUri`. Must be an absolute URI when set. |
| `ExposureUploadBatchSize` | `int` | `100` | Maximum number of exposure events sent in a single upload request. Must be greater than zero. |

### Built-in defaults

`SharpNinjaFeatureFlagOptions.Default` is a pre-built instance targeting `"truckmate"`, `"truckmate-0.0.0-stable-0"`, environment `"development"`, 5-minute refresh, and 30-second upload. It is provided for testing and local development only.

### Validation

Calling `Validate()` on the options record is required before passing it to `AddSharpNinjaFeatureFlags`. The extension method calls `Validate()` internally, so explicit calls are only necessary when constructing options outside of DI registration.

Validation enforces:
- `ProductId`, `ReleaseId`, and `Environment` are non-blank.
- `ManifestRefreshInterval` and `ExposureUploadInterval` are greater than zero.
- `ExposureUploadBatchSize` is greater than zero.
- `ProductId` appears in `SupportedProductIds`.
- `ReleaseLineage.ReleaseId` matches `ReleaseId`.
- `DeploymentEnvironment.Name` matches `Environment`.
- `DistributionBaseUri` and `ExposureUploadEndpoint` are absolute URIs when set.

---

## SharpNinjaReleaseLineage

Captures full release identity: semantic version, channel, and build number.

```csharp
public sealed record SharpNinjaReleaseLineage(
    string ReleaseId,
    string SemanticVersion,
    SharpNinjaReleaseChannel Channel,
    string Build);
```

| Property | Type | Description |
|---|---|---|
| `ReleaseId` | `string` | Immutable release identifier, must match `SharpNinjaFeatureFlagOptions.ReleaseId`. |
| `SemanticVersion` | `string` | Strict semver in `major.minor.patch` form (pre-release and build metadata allowed). |
| `Channel` | `SharpNinjaReleaseChannel` | Release channel: `Canary` (0), `Beta` (1), or `Stable` (2). |
| `Build` | `string` | Build number or identifier within the channel. |

`SharpNinjaReleaseLineage.Default` targets `"truckmate-0.0.0-stable-0"`, version `"0.0.0"`, `Stable` channel, build `"0"`.

`SharpNinjaReleaseLineage.FromReleaseId(string)` constructs a compatibility instance from a release ID string, using `"0.0.0"`, `Stable`, and build `"0"` as defaults.

The `SemanticVersion` is injected into evaluation context under the key `"SemanticVersion"`. The `Channel` becomes `"ReleaseChannel"`. The `Build` becomes `"ReleaseBuild"`. Rules in the manifest can target these keys for channel-specific rollouts.

---

## SharpNinjaReleaseChannel

```csharp
public enum SharpNinjaReleaseChannel
{
    Canary = 0,
    Beta   = 1,
    Stable = 2,
}
```

---

## SharpNinjaDeploymentEnvironment

Represents the target deployment environment. Supports built-in names and custom names when `AllowCustomEnvironments` is `true`.

```csharp
public sealed record SharpNinjaDeploymentEnvironment(string Name);
```

| Static member | `Name` value |
|---|---|
| `SharpNinjaDeploymentEnvironment.Development` | `"development"` |
| `SharpNinjaDeploymentEnvironment.Staging` | `"staging"` |
| `SharpNinjaDeploymentEnvironment.Production` | `"production"` |

`SharpNinjaDeploymentEnvironment.Create(string name)` constructs an instance for any non-blank name.

`IsBuiltIn()` returns `true` for the three built-in names.

---

## SharpNinjaExposureRetentionOptions

Controls how long exposure events are kept in the on-disk outbox before being discarded.

```csharp
public sealed record SharpNinjaExposureRetentionOptions(TimeSpan? RetentionPeriod);
```

| Property | Type | Description |
|---|---|---|
| `RetentionPeriod` | `TimeSpan?` | Retention window. `null` means indefinite. Must be greater than zero when set. |

| Static member | `RetentionPeriod` |
|---|---|
| `SharpNinjaExposureRetentionOptions.Default` | `TimeSpan.FromDays(90)` (90 days) |
| `SharpNinjaExposureRetentionOptions.Indefinite` | `null` |

---

## SharpNinjaMultiTenantOptions

Controls tenant isolation in manifest selection and exposure telemetry.

```csharp
public sealed record SharpNinjaMultiTenantOptions(
    bool Enabled,
    string TenantContextKey,
    string? DefaultTenantId = null);
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `false` | When `true`, the SDK operates in multi-tenant mode and expects a tenant ID in the evaluation context. |
| `TenantContextKey` | `string` | `"TenantId"` | The evaluation context key used to carry the tenant identifier. |
| `DefaultTenantId` | `string?` | `null` | Fixed tenant identifier used for all evaluations. Set for single-product tenants or local development. When set, the value is injected into every evaluation context and cannot be overridden by callers. |

| Static member | Description |
|---|---|
| `SharpNinjaMultiTenantOptions.SingleTenant` | `Enabled = false`, key `"TenantId"`, no default ID. |
| `SharpNinjaMultiTenantOptions.MultiTenant` | `Enabled = true`, key `"TenantId"`, no default ID. |

`DefaultTenantContextKey` constant is `"TenantId"`.

---

## Distribution URL Configuration

Point the SDK at a running Distribution service by setting `DistributionBaseUri`:

```csharp
var options = new SharpNinjaFeatureFlagOptions(
    productId: "truckmate",
    releaseId: "truckmate-1.2.0-stable-0",
    environment: "production",
    manifestRefreshInterval: TimeSpan.FromMinutes(5),
    exposureUploadInterval: TimeSpan.FromSeconds(30))
{
    DistributionBaseUri = new Uri("https://flags.example.com/"),
};
```

When `DistributionBaseUri` is set, the SDK derives the manifest fetch and exposure upload URLs from it. To override the exposure upload URL independently:

```csharp
{
    DistributionBaseUri  = new Uri("https://flags.example.com/"),
    ExposureUploadEndpoint = new Uri("https://telemetry.example.com/v1/exposures"),
}
```

Both URIs must be absolute. Relative URIs throw `InvalidOperationException` during `Validate()`.

When `DistributionBaseUri` is `null` the SDK operates fully offline: the bundled manifest and any on-disk cache are the only sources, and exposure events accumulate locally without being uploaded.

---

## Cache TTL and Refresh Behavior

The manifest cache has no time-based expiry on the disk file itself. The SDK treats the cached manifest as valid until a newer manifest arrives from the distribution service. The `ManifestRefreshInterval` controls how often the distribution service is polled, not how long the cache is trusted. The cache is always superseded by a successfully verified remote manifest.

If the distribution service returns a `304 Not Modified` response (via ETag matching), the active manifest is unchanged and the cache is not rewritten.

---

## Exposure Event Upload Options

| Option | Type | Default | Effect |
|---|---|---|---|
| `ExposureUploadInterval` | `TimeSpan` | caller-specified | Drain-and-upload cadence for the outbox. |
| `ExposureUploadBatchSize` | `int` | `100` | Maximum events per upload request. |
| `ExposureOutboxPath` | `string?` | platform default | Path to the on-disk outbox directory. |
| `ExposureUploadEndpoint` | `Uri?` | derived from `DistributionBaseUri` | Target HTTP endpoint for upload. |
| `ExposureRetention.RetentionPeriod` | `TimeSpan?` | 90 days | Events older than this are discarded. |

To disable exposure upload entirely, set `ExposureUploadEndpoint` to `null` and omit `DistributionBaseUri`. Events will accumulate on disk according to the retention policy but will never be sent.

---

## Signature Verification Options

Signature verification uses the structural algorithm by default: the bundled manifest registered via the raw JSON overload receives the placeholder signature `"bundled-development-signature"` with key ID `"bundled-development-key"` and algorithm `"structural"`. This passes structural verification but is not cryptographically secure.

For production use the signed-envelope overload:

```csharp
var envelope = new SignedManifestEnvelope(
    manifestJson: File.ReadAllText("flags/flags.json"),
    signature:    "<base64-ed25519-signature>",
    signingKeyId: "prod-key-2025",
    algorithm:    "structural");

services.AddSharpNinjaFeatureFlags(options, envelope);
```

`SignedManifestEnvelope` properties:

| Property | Type | Description |
|---|---|---|
| `ManifestJson` | `string` | Raw manifest JSON payload. |
| `Signature` | `string` | Signature over the manifest JSON. |
| `SigningKeyId` | `string` | Identifier for the verification key. |
| `Algorithm` | `string` | Signature algorithm name (currently `"structural"`). |
| `ManifestId` | `string` | SHA-256 hex of `ManifestJson`, computed automatically. |
| `ETag` | `string?` | Optional ETag for conditional HTTP refresh. |
| `PublishedAt` | `DateTimeOffset?` | Optional publication timestamp. |

---

## Multi-Tenant Configuration

### Single-tenant with a fixed tenant ID

```csharp
var options = new SharpNinjaFeatureFlagOptions(...)
{
    MultiTenant = new SharpNinjaMultiTenantOptions(
        Enabled: false,
        TenantContextKey: "TenantId",
        DefaultTenantId: "acme-corp"),
};
```

### Multi-tenant with caller-supplied tenant

```csharp
var options = new SharpNinjaFeatureFlagOptions(...)
{
    MultiTenant = SharpNinjaMultiTenantOptions.MultiTenant,
};

// At evaluation time, supply the tenant via context:
EvaluationContext context = EvaluationContext.Builder()
    .Set("TenantId", "acme-corp")
    .Build();

EvaluationResult<bool> result = client.Evaluate("some.flag", false, context);
```

When `DefaultTenantId` is set, it is injected into every evaluation context and the caller cannot override it. This prevents tenant ID spoofing in single-tenant deployments.

---

## Well-Known Evaluation Context Keys

The SDK injects the following keys into every evaluation context automatically. Manifest rules can target these keys without any extra caller setup.

| Key constant | `string` value | Injected from |
|---|---|---|
| `SharpNinjaEvaluationContextKeys.ProductId` | `"ProductId"` | `options.ProductId` |
| `SharpNinjaEvaluationContextKeys.ReleaseId` | `"ReleaseId"` | `options.ReleaseId` |
| `SharpNinjaEvaluationContextKeys.SemanticVersion` | `"SemanticVersion"` | `options.ReleaseLineage.SemanticVersion` |
| `SharpNinjaEvaluationContextKeys.ReleaseChannel` | `"ReleaseChannel"` | `options.ReleaseLineage.Channel.ToString()` |
| `SharpNinjaEvaluationContextKeys.ReleaseBuild` | `"ReleaseBuild"` | `options.ReleaseLineage.Build` |
| `SharpNinjaEvaluationContextKeys.Environment` | `"Environment"` | `options.Environment` |
| `SharpNinjaEvaluationContextKeys.TenantId` | `"TenantId"` | `options.MultiTenant.DefaultTenantId` (when set) |

Caller-supplied context values for these keys are silently discarded to prevent identity spoofing.
