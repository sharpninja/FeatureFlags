# Requirements Matrix

| Requirement ID | Type | Source | Status | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| IMPL-SDK-RUNTIME-001 | Implementation | Continued plan implementation 2026-05-14 | Implemented | `tests/SharpNinja.FeatureFlags.Tests`, `tests/SharpNinja.FeatureFlags.Evaluation.Tests`, `tests/SharpNinja.FeatureFlags.Abstractions.Tests` | SDK runtime product/release/environment context injection and exposure event plumbing for FR-1, FR-6, FR-8, FR-10, TR-5, TR-7, and TR-11. |
| IMPL-DISTRIBUTION-RUNTIME-001 | Implementation | Continued plan implementation 2026-05-14 | Implemented | `tests/SharpNinja.FeatureFlags.Distribution.Tests` | Distribution service manifest lookup, ETag/delta, product-scoped API key, exposure ingestion, and observability-ready endpoints for FR-3, FR-6, FR-8, TR-5, TR-7, TR-9, TR-10, and TR-11. |
| IMPL-ADMIN-RUNTIME-001 | Implementation | Continued plan implementation 2026-05-14 | Implemented | `tests/SharpNinja.FeatureFlags.Admin.Tests` | Admin authoring, product/environment scope validation, immutable audit, publish/promote, and RBAC metadata foundations for FR-9, FR-10, FR-11, TR-9, TR-10, and TR-11. |
| TEST-AVALONIA-SAMPLE-001 | Test | User request 2026-05-14 | Implemented | `tests/SharpNinja.FeatureFlags.Avalonia12.IntegrationTests` | Covers complete Avalonia 12 sample app, 2 Projects x 2 Features, expected output snapshots, and product-scope fallback. |
| TEST-CQRS-MCPSERVER-001 | Test | User request 2026-05-14 | Implemented | `tests/SharpNinja.FeatureFlags.Cqrs.Tests` | Carries over the CQRS library unit tests from `F:\GitHub\McpServer\tests\McpServer.Cqrs.Tests` under the `SharpNinja` namespace. |
