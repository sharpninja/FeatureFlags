# Distribution Service

The Distribution service delivers signed manifests to SDK instances and collects exposure events.

## Base URL

Development default: `http://localhost:18081`

Docker: `http://distribution:8080` (internal service name)

---

## Endpoints

### GET /

Health/identification check.

**Response:** `200 OK`, body: `SharpNinja Feature Flags Distribution` (plain text)

---

### GET /v1/manifest/{productId}/{releaseId}

Fetch the current signed manifest for a product/release/environment.

**Path parameters:**

| Parameter | Type | Description |
|---|---|---|
| `productId` | `string` | Product identifier (e.g., `truckmate`). |
| `releaseId` | `string` | Release identifier (e.g., `truckmate-1.2.0-stable-0`). |

**Query parameters:**

| Parameter | Type | Required | Description |
|---|---|---|---|
| `environment` | `string` | No | Target environment. Defaults to service default (`Development`). |

**Request headers:**

| Header | Required | Description |
|---|---|---|
| `X-SharpNinja-Api-Key` | Yes (primary) | Per-product API key. |
| `X-Api-Key` | Yes (fallback) | Generic API key (used if `X-SharpNinja-Api-Key` absent). |
| `Authorization: Bearer <token>` | Yes (fallback) | Bearer token (used if both key headers absent). |
| `If-None-Match` | No | ETag from a previous response. Returns 304 if unchanged. |
| `X-SharpNinja-Device-Attestation` | Conditional | Device attestation token. Required if policy demands it. |
| `X-SharpNinja-Device-Platform` | No | Platform hint (e.g., `iOS`, `Android`, `Windows`). |

**Response codes:**

| Code | Condition |
|---|---|
| `200 OK` | Manifest found; body is `DistributionManifest` JSON. |
| `304 Not Modified` | Client's `If-None-Match` ETag matches current ETag. Body empty. |
| `401 Unauthorized` | Missing or invalid API key. Body: `{"code":"missing_api_key"}` or `{"code":"invalid_api_key"}`. |
| `403 Forbidden` | Device attestation failed. Body: `{"code":"invalid_device_attestation"}` or custom policy code. |
| `404 Not Found` | No manifest registered for `(productId, releaseId, environment)`. |

**Response body (200):** `DistributionManifest`

```json
{
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "production",
  "json": "<signed manifest JSON string>",
  "etag": "sha256-a3f8...",
  "updatedAt": "2026-05-15T12:00:00Z"
}
```

**Response headers (200):**

| Header | Description |
|---|---|
| `ETag` | Strong entity tag: `"sha256-<64-char hex>"`. Quote marks included. |
| `Cache-Control` | Present when CDN headers enabled: `max-age=60, stale-while-revalidate=300, stale-if-error=3600`. |

---

### GET /v1/manifest/{productId}/{releaseId}/delta

Conditional manifest fetch. Returns the manifest only if it has changed since the given ETag or timestamp.

Same path parameters and auth headers as `/v1/manifest/{productId}/{releaseId}`.

**Additional query parameters:**

| Parameter | Type | Description |
|---|---|---|
| `since` | `string` | ETag or ISO-8601 UTC timestamp. Returns 304 if manifest unchanged since this value. |

**Response codes:** Same as the base manifest endpoint.

---

### POST /v1/exposure

Upload a batch of exposure events recorded by the SDK.

**Request headers:** Same API key auth as manifest endpoints.

**Request body:** `ExposureBatchRequest`

```json
{
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "production",
  "events": [
    {
      "flagKey": "dashboard.enabled",
      "resolvedValue": true,
      "matchedRuleIndex": 0,
      "contextFingerprint": "sha256-...",
      "timestamp": "2026-05-15T12:00:01.000Z"
    }
  ]
}
```

**ExposureEventRequest fields:**

| Field | Type | Required | Description |
|---|---|---|---|
| `flagKey` | `string` | Yes | The flag that was evaluated. |
| `resolvedValue` | `any` | Yes | The resolved value (any JSON type matching the flag's type). |
| `matchedRuleIndex` | `int?` | No | Zero-based index of the matched rule; `null` if default value was returned. |
| `contextFingerprint` | `string` | No | Stable hash of the evaluation context (used for deduplication). |
| `timestamp` | `string` | Yes | ISO-8601 UTC timestamp of the evaluation. |

**Response codes:**

| Code | Condition |
|---|---|
| `202 Accepted` | Batch accepted. Body: `{"accepted": <event count>}`. |
| `400 Bad Request` | Malformed batch (missing required fields, invalid JSON). |
| `401 Unauthorized` | Missing or invalid API key. |
| `403 Forbidden` | Device attestation failed. |

---

### GET /health

Liveness check.

**Response:** `200 OK`, body: `{"status":"ok"}`

No authentication required.

---

### GET /metrics

Prometheus metrics in text format 0.0.4.

**Response:** `200 OK`, content type `text/plain; version=0.0.4`

**Metric names:**

| Metric | Type | Description |
|---|---|---|
| `sharpninja_distribution_manifests` | gauge | Registered manifests count. |
| `sharpninja_distribution_auth_success_total` | counter | Authorized requests. |
| `sharpninja_distribution_auth_failure_total` | counter | Failed authorization requests. |
| `sharpninja_distribution_attestation_success_total` | counter | Passed device attestation. |
| `sharpninja_distribution_attestation_failure_total` | counter | Failed device attestation. |
| `sharpninja_distribution_attestation_skipped_total` | counter | Skipped attestation (not required). |
| `sharpninja_distribution_manifest_cache_hits_total` | counter | ETag cache hits (304 responses). |
| `sharpninja_distribution_manifest_cache_misses_total` | counter | Full manifest fetches. |
| `sharpninja_distribution_manifest_not_modified_total` | counter | 304 Not Modified responses. |
| `sharpninja_distribution_exposure_batches_total` | counter | Accepted exposure batches. |
| `sharpninja_distribution_exposure_events_total` | counter | Accepted exposure events. |
| `sharpninja_distribution_storage_mode` | info | Storage mode: `manifest` or `exposure` label. |

---

## Authentication

### API Key Validation

Keys are per-product and validated by `IProductApiKeyValidator`. Configuration:

```
Distribution__ApiKeys__truckmate__0=<key>
Distribution__ApiKeys__truckmate__1=<key>   # multiple keys supported
Distribution__ApiKeys__drivermate__0=<key>
```

Key lookup order per request:
1. `X-SharpNinja-Api-Key` header
2. `X-Api-Key` header
3. `Authorization: Bearer <token>` header (token used as key value)

On failure:
- Missing header: `401 {"code":"missing_api_key"}`
- Invalid value: `401 {"code":"invalid_api_key"}`

### Device Attestation

Optional, controlled per-request by `IDeviceAttestationPolicy.EvaluateAsync()`.

Flow:
1. Policy evaluates whether attestation is required for this request.
2. If required, `IDeviceAttestationValidator.ValidateAsync()` is called.
3. At least one registered validator must return success.
4. Failure returns `403 {"code":"invalid_device_attestation"}` or custom policy code.

Test tokens (for development/testing):

```
Distribution__DeviceAttestation__TestTokens__truckmate__0=<token>
```

Pass test tokens in the `X-SharpNinja-Device-Attestation` header.

---

## ETag Protocol

ETag format: `"sha256-<64-char lowercase hex>"` (with surrounding quotes as per HTTP spec).

The SHA-256 is computed over the canonical manifest JSON string (`DistributionManifest.Json`).

**Conditional request flow:**

```
Client                         Distribution
  |  GET /v1/manifest/...        |
  |----------------------------->|
  |  200 OK + ETag: "sha256-..." |
  |<-----------------------------|
  |                              |
  |  GET /v1/manifest/...        |
  |  If-None-Match: "sha256-..." |
  |----------------------------->|
  |  304 Not Modified            |
  |<-----------------------------|
```

The SDK sends `If-None-Match` on every refresh cycle after the first fetch.

---

## CDN Cache Headers

When `Distribution__Cdn__EnableCacheHeaders=true`:

```
Cache-Control: max-age=60, stale-while-revalidate=300, stale-if-error=3600
```

Configuration:

```
Distribution__Cdn__EnableCacheHeaders=true
Distribution__Cdn__ManifestMaxAgeSeconds=60
Distribution__Cdn__ManifestStaleWhileRevalidateSeconds=300
Distribution__Cdn__ManifestStaleIfErrorSeconds=3600
```

---

## Storage

### FileSystem (default)

Manifests are stored as JSON files on disk at `Distribution__Storage__RootPath`.

Directory layout:

```
{RootPath}/
  manifests/
    {productId}/
      {releaseId}/
        {environment}.json
  exposures/
    {productId}/
      {releaseId}/
        {environment}/
          {date}/
            {batch-id}.json
```

### InMemory

All state held in memory. Lost on restart. Use for unit tests.

```
Distribution__Storage__Mode=InMemory
```

---

## Docker Configuration

```yaml
distribution:
  image: sharpninja-featureflags-distribution:dev
  ports:
    - "18081:8080"
  environment:
    - ASPNETCORE_ENVIRONMENT=Development
    - Distribution__Storage__Mode=FileSystem
    - Distribution__Storage__RootPath=/data/distribution
    - Distribution__DefaultEnvironment=Development
    - Distribution__Cdn__EnableCacheHeaders=true
    - Distribution__Cdn__ManifestMaxAgeSeconds=60
    - Distribution__Cdn__ManifestStaleWhileRevalidateSeconds=300
    - Distribution__Cdn__ManifestStaleIfErrorSeconds=3600
    - Distribution__DeviceAttestation__Required=true
    - Distribution__ApiKeys__truckmate__0=<key>
    - Distribution__ApiKeys__drivermate__0=<key>
  volumes:
    - distribution-data:/data/distribution
```

---

## DI Registration

```csharp
builder.Services.AddSharpNinjaFeatureFlagDistribution(options =>
{
    options.DefaultEnvironment = "Development";
    options.Storage.Mode = DistributionStorageMode.FileSystem;
    options.Storage.RootPath = "/data/distribution";
    options.ApiKeys["truckmate"] = ["<key>"];
});

app.MapSharpNinjaFeatureFlagDistributionEndpoints();
```
