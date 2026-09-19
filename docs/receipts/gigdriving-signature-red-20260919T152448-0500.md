# TEST-GATEWAY-006 cross-platform signature RED

- Local time: 2026-09-19T15:24:48-05:00. FeatureFlags dependency HEAD before implementation: `bd2cca5aba27a62020d4a0e848d8fde8c22c2ca4`.
- Added `tests/SharpNinja.FeatureFlags.Manifest.Tests/SignedManifestEd25519VerifierTests.cs` with deterministic Ed25519 seed and exact LF/CRLF `JsonWriterOptions.NewLine` fixture variants. Both variants must verify; changed signed `releaseId` must fail.
- Invalid preliminary check: `dotnet test ... --no-restore` returned exit 0 but `obj/project.assets.json` was absent and `--list-tests` listed no tests. It is not counted as GREEN or RED evidence.
- Actual RED command: `dotnet test .\tests\SharpNinja.FeatureFlags.Manifest.Tests\SharpNinja.FeatureFlags.Manifest.Tests.csproj --filter FullyQualifiedName~SignedManifestEd25519VerifierTests --nologo --verbosity normal`.
- Actual result: exit 1, Total 4, Passed 3, Failed 1. The failing case was `VerifyAcceptsCanonicalSignaturesFromBothPlatforms(canonicalNewLine: "\n")` at test line 20: `Assert.True()` expected True, actual False. CRLF verification and both tamper cases passed. This confirms current Windows verifier depends on platform default newline and rejects the new LF canonical signature.
- Full raw command output was captured by PowerShell.Mcp at `F:\GitHub\McpServer\.mcpServer\tmp\PowerShell.MCP.Output\pwsh_output_20260919_152410_265_kv2gzf1l.3yw.txt`. No full suite or hostile review was run at this phase.

## Keytool RED, 2026-09-19T15:27:00-05:00

- Added `tests/TruckMate.FeatureFlags.Tests/FeatureFlagsKeytoolCanonicalizationTests.cs` before editing the keytool. It signs a copy of the actual GigDriving manifest with a disposable key, requires LF-only output, verifies that new signature, then checks the packaged legacy Production signature.
- Focused command: `dotnet test .\tests\TruckMate.FeatureFlags.Tests\TruckMate.FeatureFlags.Tests.csproj --filter FullyQualifiedName~FeatureFlagsKeytoolCanonicalizationTests --nologo --verbosity minimal`.
- RED result: exit 1, Total 1, Passed 0, Failed 1, Skipped 0. Failure at line 33: `Newly signed manifest contains CRLF instead of canonical LF.` This independently tests the CLI output bytes, not the verifier's internal helper.
