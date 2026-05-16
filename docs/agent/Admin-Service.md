# Admin Service

The Admin service manages flag drafts, governs publishing, and maintains an immutable audit trail.

## Interface: IAdminRuntimeService

Source: `src/SharpNinja.FeatureFlags.Admin/IAdminRuntimeService.cs`

### CreateDraftAsync

```csharp
ValueTask<FeatureFlagDraft> CreateDraftAsync(
    FeatureFlagDraftMutation mutation,
    CancellationToken cancellationToken = default)
```

Creates a new flag draft in the given product/environment. Fails if a draft already exists for the same `(FlagKey, EnvironmentName, ProductScope)` combination.

Required RBAC right: `Edit`.

Returns `FeatureFlagDraft` at revision 1.

Appends audit entry: `Action = Created`.

### UpdateDraftAsync

```csharp
ValueTask<FeatureFlagDraft> UpdateDraftAsync(
    FeatureFlagDraftMutation mutation,
    CancellationToken cancellationToken = default)
```

Updates an existing draft. The draft must exist.

Required RBAC right: `Edit`.

Returns `FeatureFlagDraft` at revision N+1.

Appends audit entry: `Action = Updated`.

### PublishAsync

```csharp
ValueTask<AdminAuditEntry> PublishAsync(
    FeatureFlagPublishAction action,
    CancellationToken cancellationToken = default)
```

Records a publish event for an existing draft. Does not itself deliver the manifest to the Distribution service - that is an operator step (copy the signed manifest output to Distribution storage).

Required RBAC right: `Publish`. Publishing to `production` additionally requires the `Edit` role.

Returns `AdminAuditEntry` for the publish event.

Appends audit entry: `Action = Published`.

### PromoteAsync

```csharp
ValueTask<FeatureFlagDraft> PromoteAsync(
    FeatureFlagPromotionAction action,
    CancellationToken cancellationToken = default)
```

Copies the draft from `SourceEnvironmentName` to `TargetEnvironmentName`. Source and target must be different environments. Source draft must exist.

Required RBAC right: `Promote` or `Publish`.

Returns `FeatureFlagDraft` in the target environment (revision 1 if new, existing +1 if overwriting).

Appends audit entry: `Action = Promoted`, `TargetEnvironmentName` set.

### GetDrafts

```csharp
IReadOnlyList<FeatureFlagDraft> GetDrafts()
```

Returns a snapshot of all current drafts. No RBAC enforcement at the call site; callers should filter by product grant if needed.

### GetAuditTrail

```csharp
IReadOnlyList<AdminAuditEntry> GetAuditTrail()
```

Returns all audit entries in ascending sequence order. Append-only; entries are never modified or removed.

### GetMetrics

```csharp
AdminRuntimeMetrics GetMetrics()
```

Returns lightweight counters: `DraftCount`, `AuditEntryCount`, `PublishCount`, `PromotionCount`.

---

## Data Types

### FeatureFlagDraftMutation (input)

Used by `CreateDraftAsync` and `UpdateDraftAsync`.

| Field | Type | Description |
|---|---|---|
| `FlagKey` | `string` | Flag identifier. Unique within a manifest. |
| `EnvironmentName` | `string` | Target environment (`development`, `staging`, `production` or custom if allowed). |
| `ProductScope` | `IReadOnlyCollection<string>` | Products this flag applies to. All must be in the actor's product grant. |
| `ValueType` | `string` | CLR type name: `Boolean`, `String`, `Integer`, `Double`. |
| `DefaultValue` | `string` | Serialized default value (e.g., `"false"`, `"my-string"`, `"42"`). |
| `RuleDescriptions` | `IReadOnlyCollection<string>` | CEL rule expressions. Applied in order; first match wins. |
| `Reason` | `string` | Human-readable explanation for this change. Stored in audit trail. |
| `RbacMetadata` | `AdminRbacMetadata` | Actor identity and grants. |

### FeatureFlagDraft (output)

| Field | Type | Description |
|---|---|---|
| `FlagKey` | `string` | Flag identifier. |
| `EnvironmentName` | `string` | Environment this draft targets. |
| `ProductScope` | `IReadOnlyCollection<string>` | Scoped products. |
| `ValueType` | `string` | CLR type name. |
| `DefaultValue` | `string` | Serialized default value. |
| `RuleDescriptions` | `IReadOnlyCollection<string>` | Current CEL rule list. |
| `LastReason` | `string` | Reason from the most recent mutation. |
| `LastRbacMetadata` | `AdminRbacMetadata` | Actor from the most recent mutation. |
| `Revision` | `long` | Monotonic revision counter. Starts at 1; increments on every update. |
| `LastModifiedAt` | `DateTimeOffset` | UTC timestamp of the most recent mutation. |

### FeatureFlagPublishAction (input)

| Field | Type | Description |
|---|---|---|
| `FlagKey` | `string` | Flag to publish. Draft must exist. |
| `EnvironmentName` | `string` | Environment of the draft to publish. |
| `Reason` | `string` | Publish reason. Stored in audit trail. |
| `RbacMetadata` | `AdminRbacMetadata` | Actor identity and grants. |

### FeatureFlagPromotionAction (input)

| Field | Type | Description |
|---|---|---|
| `FlagKey` | `string` | Flag to promote. Source draft must exist. |
| `SourceEnvironmentName` | `string` | Environment to copy from. |
| `TargetEnvironmentName` | `string` | Environment to copy to. Must differ from source. |
| `Reason` | `string` | Promotion reason. Stored in audit trail. |
| `RbacMetadata` | `AdminRbacMetadata` | Actor identity and grants. |

### AdminAuditEntry (output)

| Field | Type | Description |
|---|---|---|
| `Sequence` | `long` | Monotonic sequence number. Unique per runtime instance. |
| `Action` | `AdminAuditAction` | `Created`, `Updated`, `Published`, `Promoted`. |
| `FlagKey` | `string` | Subject flag. |
| `EnvironmentName` | `string` | Primary environment. |
| `TargetEnvironmentName` | `string?` | Set only for `Promoted` actions; null otherwise. |
| `ProductScope` | `IReadOnlyCollection<string>` | Product scope at action time. |
| `ValueType` | `string` | Value type at action time. |
| `DefaultValue` | `string` | Default value at action time. |
| `RuleDescriptions` | `IReadOnlyCollection<string>` | Rule list at action time. |
| `Reason` | `string` | Supplied reason. |
| `RbacMetadata` | `AdminRbacMetadata` | Actor frozen at action time. |
| `Revision` | `long` | Draft revision at action time. |
| `OccurredAt` | `DateTimeOffset` | UTC timestamp. |

### AdminRbacMetadata (embedded)

| Field | Type | Description |
|---|---|---|
| `TenantId` | `string` | Actor's tenant identifier. `*` grants cross-tenant access. |
| `PrincipalId` | `string` | Stable actor identity (maps to OIDC subject claim). |
| `ProductIds` | `IReadOnlyCollection<string>` | Products the actor is granted. `*` grants all. |
| `RoleIds` | `IReadOnlyCollection<string>` | Roles granted to the actor. `*` grants all. |

### AdminRuntimeMetrics (output)

| Field | Type | Description |
|---|---|---|
| `DraftCount` | `int` | Current number of drafts in memory. |
| `AuditEntryCount` | `int` | Total audit entries since service start. |
| `PublishCount` | `int` | Publish actions since service start. |
| `PromotionCount` | `int` | Promote actions since service start. |

---

## RBAC Model

### Roles

| Role name | Constant | Allowed operations |
|---|---|---|
| `Viewer` | `AdminRoleNames.Viewer` | `Read` |
| `Editor` | `AdminRoleNames.Editor` | `Read`, `Edit` |
| `Publisher` | `AdminRoleNames.Publisher` | `Read`, `Edit`, `Publish`, `Promote` |
| `Promoter` | `AdminRoleNames.Promoter` | `Read`, `Promote` |
| `KeyAdmin` | `AdminRoleNames.KeyAdmin` | All operations (bypasses role checks) |

### Rights

| Right | Operations granted |
|---|---|
| `Read` | `GetDrafts`, `GetAuditTrail`, `GetMetrics` |
| `Edit` | `CreateDraftAsync`, `UpdateDraftAsync` |
| `Publish` | `PublishAsync` (non-production environments) |
| `Publish` + `Edit` | `PublishAsync` to `production` |
| `Promote` | `PromoteAsync` |

### Authorization Scope

Every operation is authorized against an `AdminAuthorizationScope`:

```csharp
record AdminAuthorizationScope(
    string TenantId,
    IReadOnlyCollection<string> ProductIds,
    string? EnvironmentName);
```

Rules:
- `TenantId` in the scope must match `RbacMetadata.TenantId` (or actor has `*`).
- All `ProductIds` in the scope must appear in `RbacMetadata.ProductIds` (or actor has `*`).
- If `EnvironmentName` is `production`, `PublishAsync` requires both `Publisher` AND `Editor` roles.
- `KeyAdmin` role bypasses all role checks.

### Policy Names

ASP.NET Core authorization policy names:

| Policy constant | Policy name |
|---|---|
| `AdminPolicyNames.Read` | `SharpNinjaAdminRead` |
| `AdminPolicyNames.Edit` | `SharpNinjaAdminEdit` |
| `AdminPolicyNames.Publish` | `SharpNinjaAdminPublish` |
| `AdminPolicyNames.Promote` | `SharpNinjaAdminPromote` |
| `AdminPolicyNames.KeyAdmin` | `SharpNinjaAdminKeyAdmin` |

---

## Authentication

### Test Mode

Header-backed authentication for integration tests. Claims are supplied as request headers:

```
X-SharpNinja-TenantId: <tenantId>
X-SharpNinja-PrincipalId: <principalId>
X-SharpNinja-Products: <comma-separated product IDs>
X-SharpNinja-Roles: <comma-separated role names>
```

Enable by configuring `AdminAuthenticationMode.Test` in `AddSharpNinjaFeatureFlagsAdminRuntime`.

### OIDC Mode

Standard OpenID Connect. Claims are mapped from the token:

| Claim | Default claim type |
|---|---|
| PrincipalId | `ClaimTypes.NameIdentifier` |
| TenantId | `SharpNinjaAdminDefaults.TenantClaimType` |
| Products | `SharpNinjaAdminDefaults.ProductsClaimType` |
| Roles | `SharpNinjaAdminDefaults.RolesClaimType` |

Product and role claim values may be comma-delimited: `"truckmate,drivermate"`.

---

## Docker Configuration

```yaml
admin:
  image: sharpninja-featureflags-admin:dev
  ports:
    - "18080:8080"
  environment:
    - ASPNETCORE_ENVIRONMENT=Development
    - FeatureFlags__Products__0=truckmate
    - FeatureFlags__Products__1=drivermate
    - FeatureFlags__MultiTenant=true
```

The Admin service connects to a database (PostgreSQL or SQL Server) for durable storage of drafts and audit entries. Configure the connection string via `ConnectionStrings__Default`.

---

## Lifecycle Constraints

| Constraint | Detail |
|---|---|
| Draft must not exist for Create | `CreateDraftAsync` throws if `(FlagKey, EnvironmentName)` already has a draft |
| Draft must exist for Update/Publish/Promote | These operations throw if the draft is not found |
| Source != Target for Promote | `PromoteAsync` throws if `SourceEnvironmentName == TargetEnvironmentName` |
| Audit entries are immutable | No update or delete on the audit trail |
| Revision is monotonic | Never resets; only increases |
| Timestamps are UTC | All `DateTimeOffset` values are UTC |
