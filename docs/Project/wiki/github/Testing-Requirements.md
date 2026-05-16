# Testing Requirements (MCP Server)

This document enumerates the TEST-* identifiers referenced by the per-FR test mapping. Each entry names the implementing test class(es) (paths are relative to the repository root) and lists the FRs/TRs the test asserts.

## TEST-SDK-RUNTIME-001

**Scope:** Validates the consumer-facing SDK runtime surface: feature client behavior, audit/exposure emission, and DI registration extensions.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Tests/SharpNinjaFeatureClientAuditTests.cs`
- `tests/SharpNinja.FeatureFlags.Tests/SharpNinjaFeatureFlagServiceCollectionExtensionsTests.cs`

**Asserts:** FR-1, FR-3, FR-4, FR-6, FR-8; TR-1, TR-5, TR-11.

## TEST-ABSTRACTIONS-V1-001

**Scope:** Validates the v1 abstractions surface (options, evaluation context, product-scope attribute, custom exception types) consumed by both SDK and admin planes.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Abstractions.Tests/EvaluationContextTests.cs`
- `tests/SharpNinja.FeatureFlags.Abstractions.Tests/FeatureFlagDisabledExceptionTests.cs`
- `tests/SharpNinja.FeatureFlags.Abstractions.Tests/ProductScopeAttributeTests.cs`
- `tests/SharpNinja.FeatureFlags.Abstractions.Tests/SharpNinjaFeatureFlagOptionsTests.cs`

**Asserts:** FR-1, FR-11; TR-2, TR-5, TR-11.

## TEST-BUILD-PIPELINE-001

**Scope:** Validates the MSBuild integration that stamps ProductId/ReleaseId, embeds bundled manifests, and emits Docker hosting artifacts.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Build.Tests/BuildTargetsTests.cs`
- `tests/SharpNinja.FeatureFlags.Build.Tests/DockerHostingArtifactsTests.cs`
- `tests/SharpNinja.FeatureFlags.Build.Tests/ValidateReleaseTests.cs`

**Asserts:** FR-1, FR-2, FR-12; TR-1, TR-2, TR-4, TR-5, TR-6, TR-8, TR-11.

## TEST-MANIFEST-VALIDATION-001

**Scope:** Validates the manifest schema, signing/verification, kill-switch metadata, environment normalization, and rule predicate well-formedness checks.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Manifest.Tests/ManifestValidatorTests.cs`

**Asserts:** FR-2, FR-5, FR-7, FR-10, FR-12; TR-2, TR-3, TR-4, TR-5, TR-6, TR-8, TR-11.

## TEST-DISTRIBUTION-RUNTIME-001

**Scope:** Validates the Distribution service endpoint handler (manifest fetch, product-scoped auth, exposure event upload) and ngrok tunneling extensions.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Distribution.Tests/DistributionEndpointHandlerTests.cs`
- `tests/SharpNinja.FeatureFlags.Distribution.Tests/NgrokTunnelingExtensionsTests.cs`

**Asserts:** FR-3, FR-6, FR-8, FR-10; TR-4, TR-5, TR-6, TR-7, TR-8, TR-9, TR-10, TR-11.

## TEST-EVALUATION-RULES-001

**Scope:** Validates the CEL-dialect rule engine: deterministic evaluation, kill-switch precedence, rule-predicate validation, and percentage-bucketing helpers.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Evaluation.Tests/FeatureFlagEvaluatorTests.cs`
- `tests/SharpNinja.FeatureFlags.Evaluation.Tests/RulePredicateValidatorTests.cs`

**Asserts:** FR-4, FR-5, FR-7; TR-2, TR-3, TR-5, TR-11.

## TEST-AVALONIA-SAMPLE-001

**Scope:** Cross-platform determinism verification using the Avalonia sample application runtime, exercising the evaluator on a real desktop target.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Avalonia12.IntegrationTests/AvaloniaSampleScenarioRunnerTests.cs`
- `tests/SharpNinja.FeatureFlags.Avalonia12.IntegrationTests/AvaloniaSampleWindowTests.cs`

**Asserts:** FR-4; TR-2, TR-3, TR-5, TR-11.

## TEST-ADMIN-RUNTIME-001

**Scope:** Validates the admin runtime service: authoring workflows, audit log capture, environment promotion, and ngrok tunneling extensions.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Admin.Tests/AdminRuntimeServiceTests.cs`
- `tests/SharpNinja.FeatureFlags.Admin.Tests/NgrokTunnelingExtensionsTests.cs`

**Asserts:** FR-9, FR-10; TR-9, TR-10, TR-11.

## TEST-ADMIN-DATA-PROVIDERS-001

**Scope:** Validates admin-data provider registration across the supported persistence stores (Postgres, SQLite, SQL Server) and the environment-name normalization performed at publish time.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Admin.Data.Tests/AdminDataProviderRegistrationTests.cs`

**Asserts:** FR-9, FR-11; TR-9, TR-10, TR-11.

## TEST-DOCKER-HOSTING-001

**Scope:** Validates Docker hosting artifacts (Dockerfile/compose) produced by the build pipeline for the Admin and Distribution services.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Build.Tests/DockerHostingArtifactsTests.cs`

**Asserts:** FR-9, FR-11; TR-9, TR-10, TR-11.

## TEST-CLI-VALIDATION-001

**Scope:** Validates the `flagctl validate` command: schema validation, rule-syntax checks, type safety, and product-scope correctness in CI.

**Implementing test classes:**
- `tests/SharpNinja.FeatureFlags.Cli.Tests/FlagctlValidateCommandTests.cs`

**Asserts:** FR-12; TR-1, TR-2, TR-8, TR-11.
