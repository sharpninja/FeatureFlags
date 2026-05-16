# Admin Plane Guide

The SharpNinja Feature Flags Admin plane is the authoring and governance layer for feature flag definitions. It is responsible for flag CRUD operations, rule editing, sign-and-publish workflows, environment promotion, and the append-only audit trail that records every state change.

---

## What the Admin Plane Does

| Capability | Description |
|---|---|
| Flag drafts | Create and update product-scoped flag drafts with typed default values and CEL rule descriptions |
| Publish | Promote a draft to an environment, producing a signed manifest the Distribution service can serve |
| Promote | Copy an existing draft from one environment to another (for example, Staging to Production) |
| Audit log | Append-only record of every Created, Updated, Published, and Promoted action |
| RBAC | Role-based access control enforced on every operation via named ASP.NET Core authorization policies |
| Metrics | Prometheus-compatible `/admin/metrics` endpoint exposing draft counts, audit entry counts, publish counts, and promotion counts |

---

## Docker Compose Setup

The `docker-compose.yml` at the repository root defines both the `admin` and `distribution` services together with a PostgreSQL and a SQL Server database.

```yaml
# Start all services (admin, distribution, postgres, sqlserver)
docker compose up --build
```

The Admin service listens on host port **18080** (container port 8080):

```yaml
services:
  admin:
    build:
      context: .
      dockerfile: src/SharpNinja.FeatureFlags.Admin/Dockerfile
    image: sharpninja-featureflags-admin:dev
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080
      FeatureFlags__Products__0: TruckMate
      FeatureFlags__Products__1: DriverMate
      FeatureFlags__ReleaseLineage__Mode: SemVerChannelBuild
      FeatureFlags__Environments__AllowCustom: "true"
      FeatureFlags__Tenancy__Mode: MultiTenant
    ports:
      - "18080:8080"
```

To start only the Admin service (and its database dependencies):

```bash
docker compose up --build admin postgres sqlserver
```

To use custom Postgres or SQL Server credentials, set environment variables before starting:

```bash
FEATUREFLAGS_POSTGRES_PASSWORD=my_secret docker compose up --build
```

### Container details

- Base image: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Internal port: `8080`
- Entry point: `dotnet SharpNinja.FeatureFlags.Admin.dll`
- Diagnostics disabled in the image via `DOTNET_EnableDiagnostics=0`

---

## RBAC: Roles and Policies

The Admin plane defines five roles and five named authorization policies. Every HTTP endpoint is protected by at least one policy.

### Roles

| Role | Constant (`AdminRoleNames`) | Purpose |
|---|---|---|
| `Viewer` | `AdminRoleNames.Viewer` | Read-only access to draft state and audit trail |
| `Editor` | `AdminRoleNames.Editor` | Create and update flag drafts |
| `Publisher` | `AdminRoleNames.Publisher` | Publish drafts and promote between environments |
| `Promoter` | `AdminRoleNames.Promoter` | Promote drafts between environments |
| `KeyAdmin` | `AdminRoleNames.KeyAdmin` | Administer product API keys; bypasses all operation-specific role checks |

### Policies and which roles satisfy them

| Policy | Constant (`AdminPolicyNames`) | Satisfied by |
|---|---|---|
| `SharpNinjaAdminRead` | `AdminPolicyNames.Read` | Viewer, Editor, Publisher, Promoter, KeyAdmin |
| `SharpNinjaAdminEdit` | `AdminPolicyNames.Edit` | Editor, KeyAdmin |
| `SharpNinjaAdminPublish` | `AdminPolicyNames.Publish` | Publisher, KeyAdmin |
| `SharpNinjaAdminPromote` | `AdminPolicyNames.Promote` | Promoter, Publisher, KeyAdmin |
| `SharpNinjaAdminKeyAdmin` | `AdminPolicyNames.KeyAdmin` | KeyAdmin |

Roles are resolved from claims. The default claim type for roles is `ClaimTypes.Role` (the standard `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` URI). A comma-delimited list of roles in a single claim is supported.

### Claim type defaults

| Claim purpose | Default claim type |
|---|---|
| Principal ID | `ClaimTypes.NameIdentifier` |
| Display name | `ClaimTypes.Name` |
| Tenant ID | `sharpninja:tenant` |
| Product grants | `sharpninja:products` |
| Roles | `ClaimTypes.Role` |

All claim types are configurable via `AdminClaimsMappingOptions`.

### Authentication modes

| Mode | When to use |
|---|---|
| `Test` (default) | Non-production; identity is supplied via HTTP headers (`X-Admin-Principal`, `X-Admin-Tenant`, `X-Admin-Products`, `X-Admin-Roles`, `X-Admin-Name`) |
| `Oidc` | Production; wire an OIDC provider via the `configureAuthentication` callback in `AddSharpNinjaFeatureFlagsAdminRuntime` |

Example production wiring with OIDC:

```csharp
builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime(
    configure: options =>
    {
        options.Authentication.Mode = AdminAuthenticationMode.Oidc;
        options.Authentication.AuthenticationScheme = SharpNinjaAdminDefaults.OidcAuthenticationScheme;
        options.Authentication.Oidc.Authority = "https://login.microsoftonline.com/{tenant}/v2.0";
        options.Authentication.Oidc.ClientId = "your-client-id";
    },
    configureAuthentication: auth =>
    {
        auth.AddOpenIdConnect(SharpNinjaAdminDefaults.OidcAuthenticationScheme, oidc =>
        {
            oidc.Authority = "https://login.microsoftonline.com/{tenant}/v2.0";
            oidc.ClientId = "your-client-id";
            oidc.ResponseType = "code";
        });
    });
```

---

## Workflow: Create, Update, Publish, Promote

### 1. Create a draft

A draft is the authoring-time representation of a feature flag. Creating a draft requires the `SharpNinjaAdminEdit` policy (`Editor` or `KeyAdmin` role).

```
IAdminRuntimeService.CreateDraftAsync(FeatureFlagDraftMutation mutation)
```

Required fields in `FeatureFlagDraftMutation`:

| Field | Description |
|---|---|
| `FlagKey` | Unique key for the flag (for example, `enable-dark-mode`) |
| `EnvironmentName` | Target environment for this draft (for example, `Development`) |
| `ProductScope` | Product IDs that may evaluate this flag |
| `ValueType` | Flag value type (`boolean`, `string`, `integer`, `number`) |
| `DefaultValue` | Serialized default value |
| `RuleDescriptions` | Human-readable descriptions of the targeting rules |
| `Reason` | Reason for this change (recorded in the audit log) |
| `RbacMetadata` | Tenant, principal, product, and role context of the actor |

### 2. Update a draft

```
IAdminRuntimeService.UpdateDraftAsync(FeatureFlagDraftMutation mutation)
```

Updating increments the draft's `Revision` counter and appends an `Updated` audit entry. Rules, default values, and product scope can all be changed before publishing.

### 3. Publish

Publishing requires the `SharpNinjaAdminPublish` policy (`Publisher` or `KeyAdmin` role).

```
IAdminRuntimeService.PublishAsync(FeatureFlagPublishAction action)
```

`FeatureFlagPublishAction` fields:

| Field | Description |
|---|---|
| `FlagKey` | Flag to publish |
| `EnvironmentName` | Environment to publish to |
| `Reason` | Reason for the publish action |
| `RbacMetadata` | Actor context |

Publishing appends a `Published` audit entry. After publishing, the Distribution service can serve the resulting manifest to SDK clients.

### 4. Promote to the next environment

Promotion requires the `SharpNinjaAdminPromote` policy (`Promoter`, `Publisher`, or `KeyAdmin` role).

```
IAdminRuntimeService.PromoteAsync(FeatureFlagPromotionAction action)
```

`FeatureFlagPromotionAction` fields:

| Field | Description |
|---|---|
| `FlagKey` | Flag to promote |
| `SourceEnvironmentName` | Environment to copy from (for example, `Staging`) |
| `TargetEnvironmentName` | Environment to copy to (for example, `Production`) |
| `Reason` | Reason for the promotion |
| `RbacMetadata` | Actor context |

Promotion copies the draft from the source environment to the target environment and appends a `Promoted` audit entry.

### Typical environment progression

```
Development  ->  Staging  ->  Production
  (create/update)  (promote)    (promote)
```

---

## Audit Log

Every Admin operation appends an immutable `AdminAuditEntry` to the in-memory audit trail. Entries are never deleted, edited, or reordered.

### What is recorded

| Field | Description |
|---|---|
| `Sequence` | Monotonically increasing sequence number |
| `Action` | `Created`, `Updated`, `Published`, or `Promoted` |
| `FlagKey` | Flag that was acted upon |
| `EnvironmentName` | Primary environment for the action |
| `TargetEnvironmentName` | Target environment for promotions; `null` for all other actions |
| `ProductScope` | Product IDs at the time of the action |
| `ValueType` | Flag value type at the time of the action |
| `DefaultValue` | Serialized default value at the time of the action |
| `RuleDescriptions` | Rule descriptions at the time of the action |
| `Reason` | Reason supplied by the actor |
| `RbacMetadata` | Tenant ID, principal ID, granted products, and granted roles |
| `Revision` | Draft revision at the time of the action |
| `OccurredAt` | UTC timestamp when the entry was appended |

### Immutability guarantees

The in-memory store uses an append-only `ImmutableList<AdminAuditEntry>` internally. There is no API to delete or modify an existing entry. Sequence numbers are assigned by an `Interlocked.Increment` counter, so they are stable and gapless within a single process lifetime.

### Viewing the audit trail

The Admin runtime exposes a plaintext audit view at:

```
GET /admin/audit
```

Each line in the response follows the format:

```
{sequence} {action} {flagKey} {environmentName} [{->} {targetEnvironmentName}] {tenantId}/{principalId} {reason}
```

---

## Multi-Environment Support

The Admin plane treats environment names as arbitrary strings. Any name can be used as long as it is non-empty. The `FeatureFlags__Environments__AllowCustom: "true"` environment variable (set in `docker-compose.yml`) enables custom environment names in the full Admin host.

Built-in environment names used throughout the examples:

| Name | Purpose |
|---|---|
| `Development` | Local developer environment; default when no environment is specified |
| `Staging` | Pre-production integration environment |
| `Production` | Live customer-facing environment |

Custom environment names (for example, `QA`, `Canary`, `RegionUS`) are valid and behave identically to the built-in names.

---

## Product Catalog

The docker-compose configuration seeds two products:

```yaml
FeatureFlags__Products__0: TruckMate
FeatureFlags__Products__1: DriverMate
```

When a draft is created, its `ProductScope` field lists the product IDs that are allowed to evaluate the flag. The `flagctl` CLI validates this at publish time: if a generated binding references a `productId` that is not in the flag's `productScope`, it emits diagnostic code `SNFF0003`.

---

## DI Registration

Register the Admin runtime in `Program.cs`:

```csharp
using SharpNinja.FeatureFlags.Admin;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime();

var app = builder.Build();

app.UseSharpNinjaFeatureFlagsAdminRuntime();

app.Run();
```

`AddSharpNinjaFeatureFlagsAdminRuntime` registers:

| Service | Default implementation |
|---|---|
| `IAdminRuntimeStore` | `InMemoryAdminRuntimeStore` |
| `IAdminRbacAuthorizer` | `DefaultAdminRbacAuthorizer` |
| `IAdminActorResolver` | `ClaimsAdminActorResolver` |
| `IAdminRuntimeService` | `InMemoryAdminRuntimeService` |
| Authentication | Test scheme (header-backed) or OIDC via the optional `configureAuthentication` callback |
| Authorization policies | All five `AdminPolicyNames` policies |

### Optional configuration callback

```csharp
builder.Services.AddSharpNinjaFeatureFlagsAdminRuntime(configure: options =>
{
    // Retain publish evidence for 30 days
    options.Retention.PublishEvidenceRetentionPeriod = TimeSpan.FromDays(30);

    // Retain metric snapshots for 7 days
    options.Retention.MetricSnapshotRetentionPeriod = TimeSpan.FromDays(7);
});
```

### Runtime endpoints

`UseSharpNinjaFeatureFlagsAdminRuntime` registers the following GET endpoints:

| Path | Description |
|---|---|
| `/` | Returns `SharpNinja Feature Flags Admin` (health check) |
| `/admin/runtime` | Returns draft count, audit entry count, publish count, and promotion count |
| `/admin/audit` | Returns the full audit trail as plaintext |
| `/admin/metrics` | Returns Prometheus metrics (`text/plain; version=0.0.4`) |

### Prometheus metrics

| Metric | Type | Description |
|---|---|---|
| `sharpninja_admin_drafts` | gauge | Current number of in-memory flag drafts |
| `sharpninja_admin_audit_entries` | gauge | Total append-only audit entries |
| `sharpninja_admin_publishes` | gauge | Number of publish audit entries |
| `sharpninja_admin_promotions` | gauge | Number of promotion audit entries |
