# Distribution Service

The SharpNinja Feature Flags Distribution service is the read-path component of the feature flag ecosystem. It serves signed flag manifests to SDK clients, buffers exposure events uploaded by SDK upload workers, and exposes Prometheus metrics for observability.

---

## What the Distribution Service Does

| Capability | Description |
|---|---|
| Manifest serving | Serves signed JSON manifests keyed by `(productId, releaseId, environment)` |
| ETag caching | Issues strong ETags and respects `If-None-Match` to avoid redundant payload transfers |
| Delta endpoint | Supports time-based and ETag-based delta queries for incremental manifest updates |
| Exposure ingestion | Accepts batches of exposure events from SDK clients and stores them for analysis |
| Device attestation | Optional per-request validation of device attestation tokens |
| Prometheus metrics | Exposes runtime counters via `/metrics` |

---

## REST API Endpoints

All endpoints are registered by calling `app.MapSharpNinjaFeatureFlagDistributionEndpoints()`.

### Endpoint summary

| Method | Path | Description |
|---|---|---|
| `GET` | `/v1/manifest/{productId}/{releaseId}` | Fetch the current manifest for a product and release |
| `GET` | `/v1/manifest/{productId}/{releaseId}/delta` | Fetch a manifest delta since a given ETag or timestamp |
| `POST` | `/v1/exposure` | Submit a batch of exposure events |
| `GET` | `/health` | Health check; returns `{"status":"ok"}` |
| `GET` | `/metrics` | Prometheus metrics |
| `GET` | `/` | Returns `SharpNinja Feature Flags Distribution` |

---

### GET /v1/manifest/{productId}/{releaseId}

Returns the current signed manifest for the given product, release, and environment.

**Route parameters:**

| Parameter | Required | Description |
|---|---|---|
| `productId` | Yes | Product identifier (for example, `TruckMate`) |
| `releaseId` | Yes | Release identifier (for example, `1.2.0`) |

**Query parameters:**

| Parameter | Required | Description |
|---|---|---|
| `environment` | No | Deployment environment name. Defaults to the `DefaultEnvironment` option (`Development` if not configured). |

**Request headers:**

| Header | Required | Description |
|---|---|---|
| `X-SharpNinja-Api-Key` | Yes* | Product-scoped API key. Also accepted as `X-Api-Key` or `Authorization: Bearer <key>`. |
| `X-SharpNinja-Device-Attestation` | Conditional | Opaque device attestation token. Required when device attestation is enabled. |
| `X-SharpNinja-Device-Platform` | Conditional | Platform identifier accompanying the attestation token. |
| `If-None-Match` | No | Client's cached ETag value. Returns `304 Not Modified` when it matches. |

**Response codes:**

| Code | Meaning |
|---|---|
| `200 OK` | Manifest returned as JSON; `ETag`, `Cache-Control`, and `Last-Modified` headers set. |
| `304 Not Modified` | Client's `If-None-Match` matched the current manifest ETag; no body returned. |
| `401 Unauthorized` | API key is missing or not recognized. |
| `403 Forbidden` | API key is valid but device attestation failed. |
| `404 Not Found` | No manifest registered for the given `(productId, releaseId, environment)` tuple. |

**Example request (curl):**

```bash
curl -s \
  -H "X-SharpNinja-Api-Key: truckmate_distribution_dev_key" \
  "http://localhost:18081/v1/manifest/TruckMate/1.2.0?environment=Development"
```

**Example request with ETag caching:**

```bash
# First request - no cache
curl -v \
  -H "X-SharpNinja-Api-Key: truckmate_distribution_dev_key" \
  "http://localhost:18081/v1/manifest/TruckMate/1.2.0?environment=Development"
# Response includes: ETag: "sha256-ABCDEF..."

# Subsequent request - use cached ETag
curl -v \
  -H "X-SharpNinja-Api-Key: truckmate_distribution_dev_key" \
  -H 'If-None-Match: "sha256-ABCDEF..."' \
  "http://localhost:18081/v1/manifest/TruckMate/1.2.0?environment=Development"
# Response: HTTP 304 No Content (manifest unchanged)
```

---

### GET /v1/manifest/{productId}/{releaseId}/delta

Returns a manifest delta. The `since` query parameter accepts either an ETag value or an ISO-8601 UTC timestamp.

**Query parameters:**

| Parameter | Required | Description |
|---|---|---|
| `environment` | No | Deployment environment name. |
| `since` | No | Client's last-known ETag or ISO-8601 timestamp. Returns `304` when the manifest is unchanged. |

**Example request:**

```bash
curl -s \
  -H "X-SharpNinja-Api-Key: truckmate_distribution_dev_key" \
  "http://localhost:18081/v1/manifest/TruckMate/1.2.0/delta?environment=Development&since=2026-05-01T00:00:00Z"
```

---

### POST /v1/exposure

Accepts a batch of exposure events from an SDK upload worker.

**Request headers:**

| Header | Required | Description |
|---|---|---|
| `X-SharpNinja-Api-Key` | Yes* | Product-scoped API key. |
| `Content-Type` | Yes | Must be `application/json`. |

**Request body (JSON):**

```json
{
  "productId": "TruckMate",
  "releaseId": "1.2.0",
  "environment": "Development",
  "events": [
    {
      "flagKey": "enable-dark-mode",
      "resolvedValue": true,
      "matchedRuleIndex": 0,
      "contextFingerprint": "abc123",
      "timestamp": "2026-05-15T12:00:00Z"
    }
  ]
}
```

| Field | Required | Description |
|---|---|---|
| `productId` | Yes | Product that evaluated the flag |
| `releaseId` | Yes | Release that evaluated the flag |
| `environment` | No | Environment; defaults to the service's `DefaultEnvironment` |
| `events` | Yes | Array of exposure events; must contain at least one entry |
| `events[].flagKey` | Yes | Evaluated flag key |
| `events[].resolvedValue` | Yes | Resolved flag value (any JSON type) |
| `events[].matchedRuleIndex` | No | Zero-based index of the matched targeting rule |
| `events[].contextFingerprint` | Yes | Stable fingerprint of the evaluation context |
| `events[].timestamp` | Yes | ISO-8601 UTC timestamp |

**Response codes:**

| Code | Meaning |
|---|---|
| `202 Accepted` | Batch accepted; response body contains `{"accepted": N}` |
| `400 Bad Request` | Malformed JSON or missing required fields; response body contains `{"error": "invalid_exposure_batch"}` |
| `401 Unauthorized` | API key is missing or not recognized |
| `403 Forbidden` | Device attestation failed |

**Example request:**

```bash
curl -s -X POST \
  -H "X-SharpNinja-Api-Key: truckmate_distribution_dev_key" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "TruckMate",
    "releaseId": "1.2.0",
    "events": [
      {
        "flagKey": "enable-dark-mode",
        "resolvedValue": true,
        "contextFingerprint": "abc123",
        "timestamp": "2026-05-15T12:00:00Z"
      }
    ]
  }' \
  "http://localhost:18081/v1/exposure"
# Response: HTTP 202 {"accepted":1}
```

---

### GET /metrics

Returns Prometheus metrics in the standard text exposition format (`text/plain; version=0.0.4`).

**Metrics exposed:**

| Metric | Type | Description |
|---|---|---|
| `sharpninja_distribution_manifests` | gauge | Registered manifests visible to the service |
| `sharpninja_distribution_auth_success_total` | counter | Requests accepted by API key authorization |
| `sharpninja_distribution_auth_failure_total` | counter | Requests rejected by API key authorization |
| `sharpninja_distribution_attestation_success_total` | counter | Requests accepted by device attestation |
| `sharpninja_distribution_attestation_failure_total` | counter | Requests rejected by device attestation |
| `sharpninja_distribution_attestation_skipped_total` | counter | Requests where attestation was not required |
| `sharpninja_distribution_manifest_cache_hits_total` | counter | Manifest lookups that found a registered manifest |
| `sharpninja_distribution_manifest_cache_misses_total` | counter | Manifest lookups that found no manifest |
| `sharpninja_distribution_manifest_not_modified_total` | counter | Requests answered with HTTP 304 |
| `sharpninja_distribution_exposure_batches_total` | counter | Exposure batches accepted |
| `sharpninja_distribution_exposure_events_total` | counter | Exposure events accepted |
| `sharpninja_distribution_storage_mode` | label set | Active storage mode (`inmemory` or `filesystem`) per store |

---

## Authentication

### API key

Every manifest and exposure request must carry a product-scoped API key. The service accepts the key from any of the following locations, checked in order:

1. `X-SharpNinja-Api-Key` header (preferred)
2. `X-Api-Key` header
3. `Authorization: Bearer <key>` header

API keys are configured per product in `appsettings.json` or via environment variables:

```json
{
  "Distribution": {
    "ApiKeys": {
      "TruckMate": ["truckmate_key_1", "truckmate_key_2"],
      "DriverMate": ["drivermate_key_1"]
    }
  }
}
```

A product may have multiple active keys to support key rotation without downtime.

### Device attestation

Device attestation is optional. When `RequireDeviceAttestation` is `true`, every request must also carry:

| Header | Description |
|---|---|
| `X-SharpNinja-Device-Attestation` | Opaque attestation token issued by the device's secure enclave or attestation provider |
| `X-SharpNinja-Device-Platform` | Platform or provider identifier (for example, `android-play-integrity`, `ios-dcappcheck`) |

Test tokens can be pre-configured for development and CI environments:

```json
{
  "Distribution": {
    "DeviceAttestation": {
      "TestTokens": {
        "TruckMate": ["truckmate_attestation_dev_token"]
      }
    }
  }
}
```

---

## ETag-Based Caching Protocol

The Distribution service computes a strong ETag for each manifest using a SHA-256 hash of the manifest JSON:

```
ETag: "sha256-<HEX>"
```

SDK clients should:

1. Store the `ETag` value from the initial `200 OK` response.
2. Include `If-None-Match: <stored-ETag>` on subsequent polls.
3. When the service returns `304 Not Modified`, continue using the cached manifest.
4. When the service returns `200 OK` with a new body, replace the cached manifest and update the stored ETag.

CDN cache headers are also emitted when `EnableCdnCacheHeaders` is `true`:

```
Cache-Control: public, max-age=60, stale-while-revalidate=300, stale-if-error=3600
```

---

## Docker Compose Setup

The Distribution service listens on host port **18081** (container port 8080):

```yaml
services:
  distribution:
    build:
      context: .
      dockerfile: src/SharpNinja.FeatureFlags.Distribution/Dockerfile
    image: sharpninja-featureflags-distribution:dev
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
      Distribution__DefaultEnvironment: Development
      Distribution__Storage__Mode: FileSystem
      Distribution__Storage__RootPath: /data/distribution
      Distribution__Cdn__EnableCacheHeaders: "true"
      Distribution__Cdn__ManifestMaxAgeSeconds: "60"
      Distribution__Authorization__RequireDeviceAttestation: "true"
      Distribution__ApiKeys__TruckMate__0: truckmate_distribution_dev_key
      Distribution__ApiKeys__DriverMate__0: drivermate_distribution_dev_key
    ports:
      - "18081:8080"
    volumes:
      - featureflags-distribution:/data/distribution
```

Production API keys are supplied via environment variables with defaults:

```yaml
Distribution__ApiKeys__TruckMate__0: ${FEATUREFLAGS_TRUCKMATE_DISTRIBUTION_KEY:-truckmate_distribution_dev_key}
```

To start only the Distribution service and its dependencies:

```bash
docker compose up --build distribution postgres sqlserver
```

### Container details

- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Internal port: `8080`
- Entry point: `dotnet SharpNinja.FeatureFlags.Distribution.dll`
- Diagnostics disabled via `DOTNET_EnableDiagnostics=0`

---

## DI Registration

Register the Distribution service in `Program.cs`:

```csharp
using SharpNinja.FeatureFlags.Distribution;

var builder = WebApplication.CreateBuilder(args);

// Register from the "Distribution" configuration section
builder.Services.AddSharpNinjaFeatureFlagDistribution(
    builder.Configuration.GetSection("Distribution"));

var app = builder.Build();

app.MapSharpNinjaFeatureFlagDistributionEndpoints();

app.Run();
```

Alternatively, register with code-based configuration only (no `appsettings.json` section required):

```csharp
builder.Services.AddSharpNinjaFeatureFlagDistribution(configure: b =>
{
    b.DefaultEnvironment = "Production";
    b.StorageMode = SharpNinjaDistributionStorageMode.FileSystem;
    b.StorageRootPath = "/data/distribution";
    b.EnableCdnCacheHeaders = true;
    b.ManifestMaxAge = TimeSpan.FromSeconds(60);
    b.ProductApiKeys["TruckMate"] = ["tm-key-1", "tm-key-2"];
    b.RequireDeviceAttestation = true;
});
```

`AddSharpNinjaFeatureFlagDistribution` registers:

| Service | Default implementation |
|---|---|
| `IDistributionManifestRegistry` | `InMemoryDistributionManifestRegistry` or `FileBackedDistributionManifestRegistry` |
| `IExposureEventStore` | `InMemoryExposureEventStore` or `FileBackedExposureEventStore` |
| `IProductApiKeyValidator` | `OptionsProductApiKeyValidator` |
| `IDeviceAttestationPolicy` | `OptionsDeviceAttestationPolicy` |
| `IDeviceAttestationValidator` | `ConfiguredDeviceAttestationValidator` |
| `DistributionMetrics` | Singleton in-process counter set |
| `DistributionRequestAuthorizer` | Singleton authorizer |
| `DistributionEndpointHandler` | Singleton endpoint handler |

---

## Manifest Store Backends

The backend is selected via `StorageMode`:

| Mode | Enum value | Description |
|---|---|---|
| In-memory | `SharpNinjaDistributionStorageMode.InMemory` (default) | Manifests and exposure events stored in process memory. Data is lost on restart. Suitable for development and testing. |
| File-backed | `SharpNinjaDistributionStorageMode.FileSystem` | Manifests and exposure events stored under `StorageRootPath`. Default path: `App_Data/distribution` relative to the application base directory. Persists across restarts. |

### Configuration via `appsettings.json`

```json
{
  "Distribution": {
    "DefaultEnvironment": "Development",
    "Authorization": {
      "RequireDeviceAttestation": false
    },
    "Storage": {
      "Mode": "FileSystem",
      "RootPath": "App_Data/distribution"
    },
    "Cdn": {
      "EnableCacheHeaders": true,
      "ManifestMaxAgeSeconds": 60,
      "ManifestStaleWhileRevalidateSeconds": 300,
      "ManifestStaleIfErrorSeconds": 3600
    },
    "ApiKeys": {
      "TruckMate": ["tm-key-1"],
      "DriverMate": ["dm-key-1"]
    }
  }
}
```
