# Agent Prompt: Integrate SharpNinja Feature Flags into TruckMate

## Your task

Add SharpNinja Feature Flags v1.0.0 to the TruckMate repository using git submodule integration. Wire up the SDK, configure the build tooling, build `flagctl` from source, write a passing smoke test, and verify the full build is green before reporting done.

---

## Before you start

**Read these documents in order.** They are authoritative. Do not infer behavior from source code alone when a document covers it.

1. `submodules/FeatureFlags/AGENTS.md` (after adding the submodule) - critical constraints and file map
2. `submodules/FeatureFlags/docs/Submodule-Integration.md` - the integration procedure you will follow
3. `submodules/FeatureFlags/docs/agent/Architecture.md` - component overview and data flow
4. `submodules/FeatureFlags/docs/agent/SDK-Reference.md` - registration, options, evaluation API
5. `submodules/FeatureFlags/docs/agent/Manifest-Schema.md` - manifest JSON format and signing
6. `submodules/FeatureFlags/docs/agent/Workflows.md` - step-by-step task sequences

Alternatively, read the GitHub wiki at https://github.com/sharpninja/FeatureFlags/wiki before the submodule exists locally.

---

## Development process

Follow the **Byrd Development Process** for all code changes:

1. Write unit tests covering the acceptance criteria first.
2. Confirm tests compile and fail correctly before writing implementation.
3. Implement until all tests pass.
4. Verify the full existing test suite still passes before closing each phase.

Do not write implementation code before tests exist. Do not skip this process because the integration "seems straightforward."

---

## What you must discover first

Before writing any code, read the TruckMate repository to answer:

- What is the repo root layout? (`src/`, solution file location, `Directory.Build.props`, `Directory.Packages.props`, `global.json`)
- Does a `Directory.Packages.props` already exist? (central package management conflict risk)
- Does a `global.json` already exist? (SDK version conflict risk)
- Does `Directory.Build.props` set `TreatWarningsAsErrors`?
- Does any project already reference feature flag or OpenFeature packages?
- Where does the primary application project live, and what is its TFM?
- Is there a CI/CD pipeline file (`.yml`, `.yaml`, `Makefile`, `build.ps1`) that controls the build? What test command does it use?
- Is there an existing `flags/` directory or any `flags.json` file in the repo?

Do not proceed past discovery until you have answers to all of the above.

---

## Integration steps

### Step 1 - Add the submodule

```shell
git submodule add https://github.com/sharpninja/FeatureFlags.git submodules/FeatureFlags
git submodule update --init --recursive
```

Pin to v1.0.0:

```shell
cd submodules/FeatureFlags
git checkout v1.0.0
cd ../..
git add submodules/FeatureFlags
```

Commit: `chore: add SharpNinja.FeatureFlags v1.0.0 as submodule`

### Step 2 - Resolve MSBuild conflicts

Check for each conflict and apply the fix from `docs/Submodule-Integration.md` Part 3:

**`Directory.Packages.props` conflict:** If TruckMate uses central package management, merge the FeatureFlags package versions into TruckMate's `Directory.Packages.props`. The FeatureFlags packages to merge are listed in `submodules/FeatureFlags/Directory.Packages.props`. Watch for version conflicts on shared packages (`Microsoft.Extensions.*`, `xunit`, etc.) and prefer the higher compatible version.

**`global.json` conflict:** TruckMate's `global.json` must specify .NET SDK `>= 10.0.204`. If TruckMate is on an earlier SDK, this is a blocker - surface it rather than silently changing the SDK version. Do not change `global.json` without explicit confirmation.

**`TreatWarningsAsErrors` bleed:** If TruckMate does not already set this, add `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` (or `true` if TruckMate enforces it) in TruckMate's `Directory.Build.props` after any import of the submodule props, so TruckMate controls its own warning policy.

Commit conflict resolutions separately: `chore: resolve MSBuild property conflicts with FeatureFlags submodule`

### Step 3 - Add FeatureFlags projects to the solution

```shell
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Abstractions/SharpNinja.FeatureFlags.Abstractions.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Evaluation/SharpNinja.FeatureFlags.Evaluation.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Manifest/SharpNinja.FeatureFlags.Manifest.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags/SharpNinja.FeatureFlags.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Build/SharpNinja.FeatureFlags.Build.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Cli/SharpNinja.FeatureFlags.Cli.csproj
```

### Step 4 - Add ProjectReferences to the TruckMate application project

In the primary TruckMate application `.csproj`:

```xml
<PropertyGroup>
  <ProductId>truckmate</ProductId>
  <ReleaseId>truckmate-1.0.0-stable-0</ReleaseId>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags\SharpNinja.FeatureFlags.csproj" />
  <ProjectReference Include="..\..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\SharpNinja.FeatureFlags.Build.csproj" />
</ItemGroup>
```

Adjust the relative path from the `.csproj` file to the submodule root.

### Step 5 - Import the Build targets

The `buildTransitive` targets do **not** auto-import when using `ProjectReference`. Add the explicit import to `Directory.Build.targets` (create it at the repo root if it does not exist):

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets"
          Condition="Exists('$(MSBuildThisFileDirectory)submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets')
                     And '$(SharpNinjaFeatureFlagsManifest)' != ''" />
</Project>
```

### Step 6 - Create a minimal development manifest

Create `src/TruckMate.App/flags/flags.json` (adjust path to the application project):

```json
{
  "schemaVersion": 1,
  "productId": "truckmate",
  "releaseId": "truckmate-1.0.0-stable-0",
  "environment": "development",
  "flags": [
    {
      "key": "feature-flags.enabled",
      "type": "boolean",
      "defaultValue": false,
      "killable": false,
      "productScope": ["truckmate"],
      "rules": []
    }
  ],
  "signature": {
    "algorithm": "structural",
    "keyId": "bundled-development-key",
    "value": "bundled-development-signature"
  }
}
```

> The `structural` signature is the development-mode shortcut. It skips Ed25519 verification and is accepted only by `AddSharpNinjaFeatureFlags(options, rawJson)`. Production manifests must use a real Ed25519 signature.

Disable public key validation for now:

```xml
<!-- In the application .csproj -->
<PropertyGroup>
  <SharpNinjaFeatureFlagsRequirePublicKey>false</SharpNinjaFeatureFlagsRequirePublicKey>
</PropertyGroup>
```

### Step 7 - Build and wire flagctl

```powershell
dotnet publish submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Cli/SharpNinja.FeatureFlags.Cli.csproj `
  --configuration Release `
  --output tools/flagctl `
  --nologo
```

Wire it to MSBuild in `Directory.Build.props`:

```xml
<PropertyGroup>
  <_FlagctlExe Condition="$([MSBuild]::IsOSPlatform('Windows'))">flagctl.exe</_FlagctlExe>
  <_FlagctlExe Condition="!$([MSBuild]::IsOSPlatform('Windows'))">flagctl</_FlagctlExe>
  <SharpNinjaFeatureFlagsCliCommand>$(MSBuildThisFileDirectory)tools\flagctl\$(_FlagctlExe)</SharpNinjaFeatureFlagsCliCommand>
</PropertyGroup>
```

Add `tools/flagctl/` to `.gitignore`.

### Step 8 - Disable build-time validation for the first passing build

Until the manifest is production-signed, disable validation to avoid blocking the build:

```xml
<SharpNinjaFeatureFlagsValidateOnBuild>false</SharpNinjaFeatureFlagsValidateOnBuild>
```

Re-enable it in a follow-up task once a real Ed25519 key pair is provisioned.

### Step 9 - Write the smoke test (tests first)

Create `tests/TruckMate.FeatureFlags.Tests/FeatureFlagSmokeTests.cs`. Write at minimum:

```csharp
// Test 1: SDK registers without throwing
// Assert: ISharpNinjaFeatureClient resolves from the container

// Test 2: Evaluating the sentinel flag returns the default value
// Assert: client.Evaluate("feature-flags.enabled", false).Value == false
//         client.Evaluate("feature-flags.enabled", false).Reason == EvaluationReason.Default

// Test 3: ProductId is correct
// Assert: options.ProductId == "truckmate"

// Test 4: Unknown flag key returns default, not an exception
// Assert: client.Evaluate("does-not-exist", "fallback").Value == "fallback"
```

Use the structural-signature test manifest (same JSON as Step 6) inline in the test. Use `NullLogger<T>.Instance` for any typed logger constructor parameters.

All four tests must fail before implementation, then pass after wiring the DI registration.

### Step 10 - Register the SDK

In the TruckMate DI registration (typically `Program.cs` or a service extension):

```csharp
using SharpNinja.FeatureFlags;
using SharpNinja.FeatureFlags.Abstractions.Options;

var options = new SharpNinjaFeatureFlagOptions(
    productId: "truckmate",
    releaseId: "truckmate-1.0.0-stable-0",
    environment: builder.Environment.EnvironmentName.ToLowerInvariant(),
    manifestRefreshInterval: TimeSpan.FromMinutes(5),
    exposureUploadInterval: TimeSpan.FromSeconds(30));

string manifestJson = File.ReadAllText(
    Path.Combine(AppContext.BaseDirectory, "flags", "flags.json"));

builder.Services.AddSharpNinjaFeatureFlags(options.Validate(), manifestJson);
```

Ensure `flags/flags.json` is copied to the output directory:

```xml
<ItemGroup>
  <Content Include="flags\flags.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Step 11 - Verify

Run in this order:

```shell
# 1. Full solution compile
dotnet build TruckMate.sln --configuration Release

# 2. FeatureFlags smoke tests
dotnet test tests/TruckMate.FeatureFlags.Tests

# 3. Full TruckMate test suite (must still be green)
dotnet test TruckMate.sln

# 4. Manual flagctl validation
tools/flagctl/flagctl validate src/TruckMate.App/flags/flags.json \
  --product-id truckmate \
  --release-id truckmate-1.0.0-stable-0
```

Do not report the task as complete until all four commands exit with code 0.

---

## Acceptance criteria

- [ ] `submodules/FeatureFlags` is present and checked out at `v1.0.0`
- [ ] Solution compiles with 0 errors, 0 new warnings
- [ ] `ISharpNinjaFeatureClient` resolves from the TruckMate DI container at runtime
- [ ] All 4 smoke tests pass
- [ ] Full TruckMate test suite still passes (no regressions)
- [ ] `flagctl validate` exits 0 on the development manifest
- [ ] `tools/flagctl/` is in `.gitignore`
- [ ] No secrets, keys, or NuGet API tokens committed

---

## What NOT to do

- Do not change `global.json` to a lower SDK version. Surface it as a blocker instead.
- Do not use `AddSharpNinjaFeatureFlags()` with a hardcoded manifest path that only works on one machine. Use `AppContext.BaseDirectory` or an embedded resource.
- Do not reference `SharpNinja.FeatureFlags.Evaluation`, `SharpNinja.FeatureFlags.Manifest`, or `SharpNinja.FeatureFlags.Abstractions` directly in the application project. They are transitive through `SharpNinja.FeatureFlags`.
- Do not commit a production Ed25519 private key. The private key never leaves the signing system.
- Do not add `<Version>` attributes to `PackageReference` items in the submodule's `.csproj` files. They use central package management. If you need version overrides, add them to TruckMate's `Directory.Packages.props`.
- Do not use static loggers, `ILoggerFactory.CreateLogger<T>()`, or service locator patterns. All dependencies must be constructor-injected.
- Do not skip the smoke tests on the grounds that "the SDK already has tests." The smoke tests verify the wiring in TruckMate's DI container specifically.

---

## Open decisions (surface to the human if unresolved)

- **ReleaseId format:** The guide uses `truckmate-1.0.0-stable-0`. Confirm with the team what release ID format TruckMate uses and whether it needs to match the manifest at build time.
- **Environment name:** `builder.Environment.EnvironmentName` must be `development`, `staging`, or `production` (case-insensitive normalized). If TruckMate uses other environment names (e.g., `local`, `qa`), surface this before proceeding.
- **Manifest signing:** The development manifest uses a structural signature. Provisioning a real Ed25519 key pair for staging/production is a separate task and is not in scope here. Note where signing needs to happen and leave a TODO.
- **Distribution service:** Remote manifest refresh is disabled until `DistributionBaseUri` is configured. Leave a TODO in the registration code noting where to add this.
- **Admin plane:** Flag authoring is out of scope for this task. The initial manifest is hand-authored. Note where the Admin service URL goes when it is available.
