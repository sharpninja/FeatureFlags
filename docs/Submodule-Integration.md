# Submodule Integration Guide

This guide covers three integration paths:

1. **Git submodule + ProjectReference** - consume library source directly during development
2. **NuGet packages** - stable production reference (see README for package names)
3. **Local `flagctl` from source** - build and wire the validation CLI from the submodule

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0.204 or later (see note on `global.json`) |
| Git | any |
| PowerShell | 7+ (for `build.ps1`) |

---

## Part 1 - Add as a Git Submodule

### Add the submodule

```shell
git submodule add https://github.com/sharpninja/FeatureFlags.git submodules/FeatureFlags
git submodule update --init --recursive
```

Or, to pin to the v1.0.0 release:

```shell
git submodule add --branch v1.0.0 https://github.com/sharpninja/FeatureFlags.git submodules/FeatureFlags
```

### Update later

```shell
git submodule update --remote submodules/FeatureFlags
git add submodules/FeatureFlags
git commit -m "chore: update FeatureFlags submodule"
```

### Clone a repo that has the submodule

```shell
git clone --recurse-submodules https://your-repo.git
# or, after a plain clone:
git submodule update --init --recursive
```

---

## Part 2 - Reference Projects Directly

### 2.1 Add projects to your solution

Add only the projects your application needs. The minimal set for an application that evaluates flags and uses the Build integration:

```shell
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Abstractions/SharpNinja.FeatureFlags.Abstractions.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Evaluation/SharpNinja.FeatureFlags.Evaluation.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Manifest/SharpNinja.FeatureFlags.Manifest.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags/SharpNinja.FeatureFlags.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Build/SharpNinja.FeatureFlags.Build.csproj
```

For server-side hosting, also add:

```shell
# Admin service (authoring plane)
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Admin/SharpNinja.FeatureFlags.Admin.csproj
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Admin.Data/SharpNinja.FeatureFlags.Admin.Data.csproj

# Choose one data provider:
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Admin.Data.Postgres/SharpNinja.FeatureFlags.Admin.Data.Postgres.csproj
# OR
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Admin.Data.SqlServer/SharpNinja.FeatureFlags.Admin.Data.SqlServer.csproj

# Distribution service
dotnet sln add submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Distribution/SharpNinja.FeatureFlags.Distribution.csproj
```

### 2.2 Add ProjectReferences to your app project

```xml
<!-- YourApp.csproj -->
<ItemGroup>
  <!-- Public contract types -->
  <ProjectReference Include="..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Abstractions\SharpNinja.FeatureFlags.Abstractions.csproj" />

  <!-- Runtime SDK (evaluation, caching, refresh, exposure) -->
  <ProjectReference Include="..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags\SharpNinja.FeatureFlags.csproj" />

  <!-- Build integration - see section 2.3 for the required extra step -->
  <ProjectReference Include="..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\SharpNinja.FeatureFlags.Build.csproj" />
</ItemGroup>

<PropertyGroup>
  <!-- Required when a manifest file is present -->
  <ProductId>truckmate</ProductId>
  <ReleaseId>truckmate-1.2.0-stable-0</ReleaseId>
</PropertyGroup>
```

### 2.3 Import the Build targets manually

> **Important:** The MSBuild `buildTransitive` mechanism only auto-imports targets when the project is consumed as a **NuGet package**. When using `ProjectReference`, you must import the targets file explicitly.

Add this to your app project file (or to a shared `Directory.Build.targets` in your repo root):

```xml
<!-- YourApp.csproj or Directory.Build.targets -->
<Import Project="..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets"
        Condition="Exists('..\submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets')" />
```

Adjust the relative path to match where your `.csproj` lives relative to the submodule root.

If you have multiple projects that consume the Build integration, put the `<Import>` in `Directory.Build.targets` once:

```xml
<!-- Directory.Build.targets at your repo root -->
<Project>
  <Import Project="$(MSBuildThisFileDirectory)submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets"
          Condition="Exists('$(MSBuildThisFileDirectory)submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets')
                     And '$(SharpNinjaFeatureFlagsManifest)' != ''" />
</Project>
```

The `SharpNinjaFeatureFlagsManifest` condition prevents the targets from activating on projects that have no manifest.

### 2.4 Dependency graph (what each project pulls in)

```
YourApp
  SharpNinja.FeatureFlags          <- SDK entry point
    SharpNinja.FeatureFlags.Abstractions
    SharpNinja.FeatureFlags.Evaluation
  SharpNinja.FeatureFlags.Manifest  <- signing verification
    SharpNinja.FeatureFlags.Abstractions
    SharpNinja.FeatureFlags.Evaluation
  SharpNinja.FeatureFlags.Build     <- MSBuild integration (build-time only)
    SharpNinja.FeatureFlags.Abstractions
```

You do **not** need to reference `Evaluation` or `Manifest` directly; they are transitive through `SharpNinja.FeatureFlags`.

---

## Part 3 - Handle MSBuild Property Conflicts

### 3.1 Central package management (`Directory.Packages.props`)

FeatureFlags uses `ManagePackageVersionsCentrally=true` with its own `Directory.Packages.props`. If your repo also uses central package management, MSBuild will merge both files, which can cause version conflicts.

**Option A - Isolate with a nested Directory.Build.props**

Create `submodules/FeatureFlags/Directory.Build.props` to shadow the root:

```xml
<!-- submodules/FeatureFlags/Directory.Build.props -->
<Project>
  <!-- Stops your repo's root Directory.Build.props from reaching into the submodule -->
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

> Git will show this file as an untracked change inside the submodule. Add it to your outer repo's `.gitignore` pattern or commit it to your repo's tracked state via a patch/overlay approach.

**Option B - Merge package versions**

Copy the package versions from `submodules/FeatureFlags/Directory.Packages.props` into your own `Directory.Packages.props`. When the submodule updates, re-sync versions. This is more maintenance work but gives full control.

**Option C - Disable central versioning for the submodule projects**

In `submodules/FeatureFlags/Directory.Build.props` (same shadow file as Option A):

```xml
<PropertyGroup>
  <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
</PropertyGroup>
```

This allows the submodule's `.csproj` files to carry their own versions. Requires the submodule projects to have `Version` attributes on all `PackageReference` items (they currently do not - they rely on central versioning). Not recommended unless you are patching the submodule.

### 3.2 SDK version (`global.json`)

FeatureFlags pins .NET SDK `10.0.204` with `rollForward: latestFeature`. If your repo has its own `global.json`, the SDK resolution uses the file **closest to the project being built**.

- Your root `global.json` governs your projects.
- `submodules/FeatureFlags/global.json` governs FeatureFlags projects when built from within the submodule directory.
- When both are in the same solution, the SDK selected is the one from your repo root. Ensure it satisfies `>= 10.0.204`.

### 3.3 `TreatWarningsAsErrors`

FeatureFlags sets `TreatWarningsAsErrors=true` globally via its `Directory.Build.props`. This propagates to all projects in the solution when MSBuild traverses upward.

To prevent this from affecting your own projects, reset it in your `Directory.Build.props`:

```xml
<!-- Your repo's Directory.Build.props -->
<Project>
  <!-- Import FeatureFlags props first if you want to inherit other settings -->
  <Import Project="submodules\FeatureFlags\Directory.Build.props" Condition="Exists('submodules\FeatureFlags\Directory.Build.props')" />

  <PropertyGroup>
    <!-- Re-apply your repo's preference, overriding the submodule's setting -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## Part 4 - Build and Install `flagctl` from Source

`flagctl` is the manifest validation CLI. It is a plain .NET console exe (not a `dotnet tool` package). Installing it from source means building the Cli project and making the output available on `PATH` or pointing the MSBuild property at it.

### 4.1 Build flagctl

```powershell
# From your repo root
dotnet build submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Cli/SharpNinja.FeatureFlags.Cli.csproj `
  --configuration Release `
  --output submodules/FeatureFlags/tools/flagctl
```

The output directory will contain `flagctl.exe` (Windows) or `flagctl` (Linux/macOS).

### 4.2 Publish as a self-contained single file (recommended for CI)

```powershell
dotnet publish submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Cli/SharpNinja.FeatureFlags.Cli.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output tools/flagctl
```

Replace `win-x64` with `linux-x64` or `osx-x64` as appropriate.

### 4.3 Add to PATH (global, per-session)

**Windows PowerShell:**

```powershell
$env:PATH += ";$PWD\tools\flagctl"
```

**Linux/macOS:**

```shell
export PATH="$PATH:$(pwd)/tools/flagctl"
```

To make this permanent, add the directory to your system or user PATH environment variable.

### 4.4 Wire into MSBuild (without PATH)

Set `SharpNinjaFeatureFlagsCliCommand` to the full path so the Build target can invoke it without requiring it to be on `PATH`:

```xml
<!-- YourApp.csproj or Directory.Build.props -->
<PropertyGroup>
  <SharpNinjaFeatureFlagsCliCommand>$(MSBuildThisFileDirectory)..\tools\flagctl\flagctl</SharpNinjaFeatureFlagsCliCommand>
</PropertyGroup>
```

Windows with `.exe` suffix:

```xml
<SharpNinjaFeatureFlagsCliCommand>$(MSBuildThisFileDirectory)..\tools\flagctl\flagctl.exe</SharpNinjaFeatureFlagsCliCommand>
```

Or compute it conditionally:

```xml
<PropertyGroup>
  <_FlagctlName Condition="$([MSBuild]::IsOSPlatform('Windows'))">flagctl.exe</_FlagctlName>
  <_FlagctlName Condition="!$([MSBuild]::IsOSPlatform('Windows'))">flagctl</_FlagctlName>
  <SharpNinjaFeatureFlagsCliCommand>$(MSBuildThisFileDirectory)..\tools\flagctl\$(_FlagctlName)</SharpNinjaFeatureFlagsCliCommand>
</PropertyGroup>
```

### 4.5 Run flagctl directly

```shell
# Validate a manifest
flagctl validate flags/flags.json \
  --product-id truckmate \
  --release-id truckmate-1.2.0-stable-0 \
  --public-key flags/public-key.ed25519

# Show help
flagctl --help
flagctl validate --help
```

Exit codes: `0` = valid, `1` = validation errors (stderr lists them), `2` = usage error or file not found.

### 4.6 Automate the build in your NUKE/MSBuild pipeline

Add a build step before your first compile:

```powershell
# build.ps1 or equivalent
$flagctlProject = "submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Cli/SharpNinja.FeatureFlags.Cli.csproj"
$flagctlOutput  = "tools/flagctl"

dotnet publish $flagctlProject `
  --configuration Release `
  --output $flagctlOutput `
  --nologo

$env:PATH += ";$PWD/$flagctlOutput"
```

---

## Part 5 - Disable Build-Time Validation (Optional)

If your CI builds without `flagctl` available and you want to validate separately, disable the validation target:

```xml
<PropertyGroup>
  <SharpNinjaFeatureFlagsValidateOnBuild>false</SharpNinjaFeatureFlagsValidateOnBuild>
</PropertyGroup>
```

Then run validation as a dedicated CI step:

```shell
flagctl validate flags/flags.json \
  --product-id $(ProductId) \
  --release-id $(ReleaseId) \
  --public-key flags/public-key.ed25519
```

---

## Complete Example Layout

```
YourRepo/
  .gitmodules
  Directory.Build.props         <- merge/override submodule settings
  Directory.Build.targets       <- Import SharpNinja.FeatureFlags.Build.targets
  Directory.Packages.props      <- merged or isolated package versions
  global.json                   <- SDK >= 10.0.204
  tools/
    flagctl/
      flagctl.exe               <- built from source (gitignored)
  submodules/
    FeatureFlags/               <- git submodule
      src/
        ...
  src/
    YourApp/
      YourApp.csproj
      flags/
        flags.json              <- signed manifest
        public-key.ed25519      <- Ed25519 public key
  YourRepo.sln
```

### `.gitignore` additions

```gitignore
# Built flagctl binary
tools/flagctl/

# Shadow files you create inside the submodule
submodules/FeatureFlags/Directory.Build.props
```

### Minimum `Directory.Build.props`

```xml
<Project>
  <PropertyGroup>
    <!-- SDK identity for all apps in this repo -->
    <ProductId>truckmate</ProductId>
    <ReleaseId>truckmate-1.2.0-stable-0</ReleaseId>

    <!-- Point to the locally built flagctl -->
    <_FlagctlExe Condition="$([MSBuild]::IsOSPlatform('Windows'))">flagctl.exe</_FlagctlExe>
    <_FlagctlExe Condition="!$([MSBuild]::IsOSPlatform('Windows'))">flagctl</_FlagctlExe>
    <SharpNinjaFeatureFlagsCliCommand>$(MSBuildThisFileDirectory)tools\flagctl\$(_FlagctlExe)</SharpNinjaFeatureFlagsCliCommand>
  </PropertyGroup>
</Project>
```

### Minimum `Directory.Build.targets`

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets"
          Condition="Exists('$(MSBuildThisFileDirectory)submodules\FeatureFlags\src\SharpNinja.FeatureFlags.Build\buildTransitive\SharpNinja.FeatureFlags.Build.targets')
                     And '$(SharpNinjaFeatureFlagsManifest)' != ''" />
</Project>
```

---

## Quick Reference

| Task | Command |
|---|---|
| Add submodule | `git submodule add https://github.com/sharpninja/FeatureFlags.git submodules/FeatureFlags` |
| Init after clone | `git submodule update --init --recursive` |
| Update submodule | `git submodule update --remote submodules/FeatureFlags` |
| Build flagctl | `dotnet publish submodules/FeatureFlags/src/SharpNinja.FeatureFlags.Cli/... -o tools/flagctl` |
| Validate manifest | `flagctl validate flags/flags.json --product-id <id> --release-id <id>` |
| Disable build validation | Set `<SharpNinjaFeatureFlagsValidateOnBuild>false</SharpNinjaFeatureFlagsValidateOnBuild>` |
| Skip public key check | Set `<SharpNinjaFeatureFlagsRequirePublicKey>false</SharpNinjaFeatureFlagsRequirePublicKey>` |
