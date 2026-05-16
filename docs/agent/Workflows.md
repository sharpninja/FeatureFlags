# Workflows

Step-by-step task sequences for common operations. Each workflow lists the exact calls to make and the constraints to check.

---

## 1. Create and Publish a New Flag

**Goal:** Author a new feature flag, validate it, and make it available to SDK instances.

### Step 1 - Draft the flag

Call `IAdminRuntimeService.CreateDraftAsync`:

```csharp
var mutation = new FeatureFlagDraftMutation
{
    FlagKey = "dashboard.v2.enabled",
    EnvironmentName = "development",
    ProductScope = ["truckmate"],
    ValueType = "Boolean",
    DefaultValue = "false",
    RuleDescriptions =
    [
        "context.user.role == 'admin'",
        "bucket(context.user.id, 0.1)"
    ],
    Reason = "Gradual rollout of dashboard v2",
    RbacMetadata = new AdminRbacMetadata
    {
        TenantId = "internal",
        PrincipalId = "author-user-id",
        ProductIds = ["truckmate"],
        RoleIds = ["Editor"]
    }
};

FeatureFlagDraft draft = await admin.CreateDraftAsync(mutation);
// draft.Revision == 1
```

**Constraints:**
- Draft must not already exist for `(FlagKey, EnvironmentName)`.
- Actor must have `Edit` right (roles: `Editor`, `Publisher`, `KeyAdmin`).
- All `ProductScope` entries must be in the actor's `ProductIds` grant.

### Step 2 - Refine the draft (optional)

If the rules need adjustment, call `UpdateDraftAsync` with a new `FeatureFlagDraftMutation`. Same fields; `Revision` increments.

### Step 3 - Produce the signed manifest JSON

The Admin service produces a draft snapshot. An operator (or automated pipeline) assembles the manifest JSON from the current drafts for the target `(productId, releaseId, environment)` and signs it with the Ed25519 private key:

```json
{
  "schemaVersion": 1,
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "development",
  "flags": [ ... ],
  "signature": {
    "algorithm": "Ed25519",
    "keyId": "truckmate-dev-2026-05",
    "value": "<base64 64-byte signature>"
  }
}
```

### Step 4 - Validate with flagctl

```shell
flagctl validate flags/flags.json \
  --product-id truckmate \
  --release-id truckmate-1.2.0-stable-0 \
  --public-key flags/public-key.ed25519
```

Exit code 0 = valid. Exit code 1 = validation errors (see stderr). Exit code 2 = usage error.

### Step 5 - Record the publish

Call `IAdminRuntimeService.PublishAsync`:

```csharp
var publishAction = new FeatureFlagPublishAction
{
    FlagKey = "dashboard.v2.enabled",
    EnvironmentName = "development",
    Reason = "Releasing for internal QA",
    RbacMetadata = new AdminRbacMetadata
    {
        TenantId = "internal",
        PrincipalId = "publisher-user-id",
        ProductIds = ["truckmate"],
        RoleIds = ["Publisher"]
    }
};

AdminAuditEntry entry = await admin.PublishAsync(publishAction);
```

**Constraints:**
- Actor must have `Publish` right.
- Publishing to `production` additionally requires `Edit` role.

### Step 6 - Load manifest into Distribution service

Copy the signed manifest JSON file to the Distribution service storage path:

```
{Distribution__Storage__RootPath}/manifests/truckmate/truckmate-1.2.0-stable-0/development.json
```

SDK instances will fetch the updated manifest on their next `ManifestRefreshInterval` poll.

---

## 2. Promote a Flag from Staging to Production

**Goal:** Copy a validated staging flag to production.

### Step 1 - Verify staging draft exists

```csharp
IReadOnlyList<FeatureFlagDraft> drafts = admin.GetDrafts();
FeatureFlagDraft? stagingDraft = drafts.FirstOrDefault(d =>
    d.FlagKey == "dashboard.v2.enabled" &&
    d.EnvironmentName == "staging");

if (stagingDraft is null)
{
    throw new InvalidOperationException("Staging draft not found.");
}
```

### Step 2 - Promote

```csharp
var promotionAction = new FeatureFlagPromotionAction
{
    FlagKey = "dashboard.v2.enabled",
    SourceEnvironmentName = "staging",
    TargetEnvironmentName = "production",
    Reason = "Staged validation complete; promoting to production",
    RbacMetadata = new AdminRbacMetadata
    {
        TenantId = "internal",
        PrincipalId = "promoter-user-id",
        ProductIds = ["truckmate"],
        RoleIds = ["Promoter"]
    }
};

FeatureFlagDraft productionDraft = await admin.PromoteAsync(promotionAction);
```

**Constraints:**
- Source environment must have an existing draft.
- `SourceEnvironmentName != TargetEnvironmentName`.
- Actor must have `Promote` or `Publish` right.

### Step 3 - Build, sign, and validate the production manifest

Same as steps 3-4 in the create workflow. The production manifest must be separately signed; you cannot use the staging manifest directly.

### Step 4 - Publish the promotion

```csharp
await admin.PublishAsync(new FeatureFlagPublishAction
{
    FlagKey = "dashboard.v2.enabled",
    EnvironmentName = "production",
    Reason = "Promoted from staging",
    RbacMetadata = new AdminRbacMetadata
    {
        TenantId = "internal",
        PrincipalId = "publisher-user-id",
        ProductIds = ["truckmate"],
        // Publishing to production requires Publisher AND Editor
        RoleIds = ["Publisher", "Editor"]
    }
});
```

### Step 5 - Deploy to Distribution service

Copy the signed production manifest to:

```
{RootPath}/manifests/truckmate/truckmate-1.2.0-stable-0/production.json
```

---

## 3. Evaluate a Flag in Application Code

```csharp
// Resolve from DI
ISharpNinjaFeatureClient client = services.GetRequiredService<ISharpNinjaFeatureClient>();

// Build context
EvaluationContext context = EvaluationContext.Builder()
    .Set("user.id", currentUser.Id)
    .Set("user.role", currentUser.Role)
    .Set("tenant.id", currentUser.TenantId)
    .Set("app.version", AppVersion.Current)
    .Build();

// Evaluate
EvaluationResult<bool> result = client.Evaluate("dashboard.v2.enabled", defaultValue: false, context);

if (result.Value)
{
    RenderDashboardV2();
}
else
{
    RenderDashboardV1();
}

// Optional: inspect reason
switch (result.Reason)
{
    case EvaluationReason.RuleMatch:
        // result.RuleIndex tells which rule matched
        break;
    case EvaluationReason.Default:
        // No rule matched; default was returned
        break;
    case EvaluationReason.Disabled:
        // Kill switch fired; flag is off
        break;
    case EvaluationReason.Error:
        // result.ErrorMessage has detail
        break;
}
```

---

## 4. Fetch Manifest from Distribution Service

The SDK handles this automatically. For direct integration or testing:

```http
GET /v1/manifest/truckmate/truckmate-1.2.0-stable-0?environment=production
X-SharpNinja-Api-Key: <api-key>
If-None-Match: "sha256-<previous-etag>"
```

**Success response (200):**

```json
{
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "production",
  "json": "{ ... signed manifest JSON ... }",
  "etag": "sha256-a3f8...",
  "updatedAt": "2026-05-15T12:00:00Z"
}
```

Parse `json` field and verify the Ed25519 signature using the public key before use.

**Not-modified response (304):** Empty body. Use cached manifest.

---

## 5. Upload Exposure Events

Exposure events are sent automatically by the SDK. For direct integration:

```http
POST /v1/exposure
X-SharpNinja-Api-Key: <api-key>
Content-Type: application/json

{
  "productId": "truckmate",
  "releaseId": "truckmate-1.2.0-stable-0",
  "environment": "production",
  "events": [
    {
      "flagKey": "dashboard.v2.enabled",
      "resolvedValue": true,
      "matchedRuleIndex": 1,
      "contextFingerprint": "sha256-abc123",
      "timestamp": "2026-05-15T12:01:00.000Z"
    }
  ]
}
```

**Success response (202):**

```json
{ "accepted": 1 }
```

---

## 6. Validate Manifest in CI

Add to CI pipeline after building the project. `flagctl` must be installed as a global tool:

```shell
dotnet tool install -g SharpNinja.FeatureFlags.Cli
```

Validation step:

```shell
flagctl validate flags/flags.json \
  --product-id $(ProductId) \
  --release-id $(ReleaseId) \
  --public-key flags/public-key.ed25519
```

Exit code:

| Code | Meaning |
|---|---|
| `0` | Valid. |
| `1` | Validation errors (stderr lists them with diagnostic codes). |
| `2` | Usage error or file not found. |

---

## 7. Query Admin Audit Trail

```csharp
IReadOnlyList<AdminAuditEntry> trail = admin.GetAuditTrail();

// Filter for a specific flag
IEnumerable<AdminAuditEntry> flagHistory = trail
    .Where(e => e.FlagKey == "dashboard.v2.enabled")
    .OrderBy(e => e.Sequence);

// Find all production publishes
IEnumerable<AdminAuditEntry> prodPublishes = trail
    .Where(e => e.Action == AdminAuditAction.Published && e.EnvironmentName == "production");

// Find promotions to production
IEnumerable<AdminAuditEntry> promotions = trail
    .Where(e => e.Action == AdminAuditAction.Promoted && e.TargetEnvironmentName == "production");
```

The audit trail is append-only and ordered by `Sequence`. Entries are never modified or removed.

---

## 8. Write a Test Using the SDK

Follow the Byrd Development Process: write tests before implementation.

```csharp
// Test manifest with structural signature (development path - no real Ed25519 needed in tests)
const string TestManifest = """
{
  "schemaVersion": 1,
  "productId": "truckmate",
  "releaseId": "truckmate-0.0.0-stable-0",
  "environment": "development",
  "flags": [
    {
      "key": "my-flag",
      "type": "boolean",
      "defaultValue": false,
      "killable": false,
      "productScope": ["truckmate"],
      "rules": [
        { "when": "context.user.role == 'admin'", "value": true }
      ]
    }
  ],
  "signature": {
    "algorithm": "structural",
    "keyId": "bundled-development-key",
    "value": "bundled-development-signature"
  }
}
""";

[Fact]
public void AdminRoleGetsTrue()
{
    ServiceCollection services = new();
    services.AddSharpNinjaFeatureFlags(SharpNinjaFeatureFlagOptions.Default, TestManifest);
    ServiceProvider provider = services.BuildServiceProvider();
    ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

    EvaluationContext ctx = EvaluationContext.Builder().Set("user.role", "admin").Build();
    EvaluationResult<bool> result = client.Evaluate("my-flag", false, ctx);

    Assert.True(result.Value);
    Assert.Equal(EvaluationReason.RuleMatch, result.Reason);
    Assert.Equal(0, result.RuleIndex);
}

[Fact]
public void NonAdminGetsDefault()
{
    ServiceCollection services = new();
    services.AddSharpNinjaFeatureFlags(SharpNinjaFeatureFlagOptions.Default, TestManifest);
    ServiceProvider provider = services.BuildServiceProvider();
    ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();

    EvaluationResult<bool> result = client.Evaluate("my-flag", false);

    Assert.False(result.Value);
    Assert.Equal(EvaluationReason.Default, result.Reason);
}
```

Use `NullLogger<T>.Instance` for any typed logger constructor parameters in tests.
