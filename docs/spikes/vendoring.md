# Spike F: vendoring and analyzer loading

## Question

Does the vendoring mechanism work? A consumer repo gets a generated `Directory.Build.targets` that imports
`build/dvx/Dvx.targets`. That file must (1) load a prebuilt `Dvx.Analyzers.dll` by path into plugin and test
projects, (2) make plugin internals visible to `<AssemblyName>.Tests`, (3) add FakeItEasy to test projects
only, and (4) run no dvx command. Does the analyzer, compiled against Roslyn 4.8.0, load without CS8032 or
AD0001? (SPEC 4.2, 4.3, 4.5, D28.)

## Method

Code: `spikes/vendoring/` (throwaway, not in `dvx.sln`). Re-run steps in its README.

- `analyzer/`: netstandard2.0, `Microsoft.CodeAnalysis.CSharp` 4.8.0 (`PrivateAssets=all`),
  `EnforceExtendedAnalyzerRules=true`, assembly name `Dvx.Analyzers`. One rule `DVXSPIKE001` (warning) on any
  class whose name ends with `Plugin`. It inherits the dvx root props, so it needed `NoWarn RS2008` (release
  tracking) because the root has `TreatWarningsAsErrors`.
- `consumer/`: isolated from the dvx root (own `Directory.Build.props`, central package management ON via own
  `Directory.Packages.props`; no `TreatWarningsAsErrors` so the warning stays visible). Generated-style
  `Directory.Build.targets` imports `build/dvx/Dvx.targets`; `build/dvx/analyzers/Dvx.Analyzers.dll` is the
  committed copy of the built analyzer. Projects: `Contoso.Plugins` (net462, `DvxProjectKind=Plugin`, only
  `Microsoft.CrmSdk.CoreAssemblies`, internal class `AccountPlugin`) and `Contoso.Plugins.Tests` (net462,
  MSTest, `DvxProjectKind=Test`, project reference; the test does `new AccountPlugin()` and
  `A.Fake<IOrganizationService>()`).
- Environment: Linux container, .NET SDK `8.0.426`, compiler `csc` `4.11.0-3.25569.22`
  (`sdk/8.0.426/Roslyn/bincore/csc.dll`). net462 reference assemblies via the SDK's implicit
  `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 (build-only).

## Result

1. Analyzer diagnostic, clean `dotnet build Contoso.sln` (0 errors, 2 warnings, no CS8032, no AD0001; also
   zero matches for either id with `-v:d`):

```
Contoso.Plugins/AccountPlugin.cs(7,20): warning DVXSPIKE001: Class 'AccountPlugin' ends with 'Plugin'
Contoso.Plugins.Tests/AccountPluginTests.cs(21,18): warning DVXSPIKE001: Class 'HelperPlugin' ends with 'Plugin'
Build succeeded.  2 Warning(s)  0 Error(s)
```

2. Internals visible. Positive: the test project compiles `new AccountPlugin()` and `plugin.Greeting` (both
   `internal`); the generated AssemblyInfo contains
   `[assembly: InternalsVisibleTo("Contoso.Plugins.Tests")]`. Negative (`-p:DvxSpikeNoIvt=true` skips the item):

```
AccountPluginTests.cs(13,30): error CS0122: 'AccountPlugin' is inaccessible due to its protection level
Build FAILED.
```
   Restored: build succeeds again. This is a compile-time check only. net462 tests cannot execute on Linux,
   so test execution is not confirmed.

3. FakeItEasy only in the test project: `Contoso.Plugins.Tests/obj/project.assets.json` lists
   `FakeItEasy >= 9.0.1` in `projectFileDependencyGroups` and has `FakeItEasy/9.0.1`; the
   `Contoso.Plugins` assets file has 0 occurrences of `FakeItEasy`. The test compiles
   `A.Fake<Microsoft.Xrm.Sdk.IOrganizationService>()`, which proves the reference arrives.

4. Analyzer loaded from the vendored path. csc received
   `/analyzer:.../consumer/build/dvx/analyzers/Dvx.Analyzers.dll`; `dotnet build -getItem:Analyzer` shows
   Identity = that path with `DefiningProjectFullPath` = `build/dvx/Dvx.targets`. The same item appears in the
   test project. The diagnostics in check 1 prove it executed.

5. Root `dotnet build`: 0 warnings, 0 errors. Root `dotnet test`: 1 passed (`Dvx.Cli.Tests`). The spikes are
   not in `dvx.sln`; the consumer's own `Directory.Build.props` stops the root props from applying to it.

## Decision

Provisional go: `dotnet build` with SDK 8.0.426 works for all four behaviours. The M1 gate (Spike F) is not
fully met until the VS 2026 and CI image rows below are confirmed by the user. A no-go would apply if either
shows CS8032 or the analyzer does not load; fallback in that case is to rebuild against an older Roslyn
(D28 revisit).

## Confirmed vs inferred

| Item | Status |
|---|---|
| `dotnet build`, .NET SDK 8.0.426, Linux | CONFIRMED |
| Analyzer loads by path; no CS8032 / AD0001 | CONFIRMED (SDK csc 4.11) |
| InternalsVisibleTo item works (compile positive and CS0122 negative) | CONFIRMED |
| FakeItEasy only in test project, central package management on | CONFIRMED |
| Test execution on net462 (Windows) | NOT CONFIRMED |
| Visual Studio 2026 (Error List, IntelliSense, no CS8032) | NOT CONFIRMED. Steps: spikes/vendoring/README.md |
| Real CI image (`dotnet --info`, build, test, warnings as seen) | NOT CONFIRMED. Steps: spikes/vendoring/README.md |
| Consumer without central package management | INFERRED NOT WORKING (see proposal 3) |

## Proposals for Task 18 (Dvx.targets shape)

1. Layout: generated `Directory.Build.targets` is only an `<Import Project="$(MSBuildThisFileDirectory)build/dvx/Dvx.targets" />`
   (use `$(MSBuildThisFileDirectory)` inside Dvx.targets for the analyzer path, so it works from any project
   directory). Define `DvxAnalyzerPath` once.
2. Project identification: use the existing `DvxProjectKind` property, with values `Plugin` (SPEC 4.2) and a new
   `Test`. One condition each (`Plugin Or Test` for the analyzer, `Plugin` for IVT, `Test` for FakeItEasy).
   `dvx new` writes the marker for both. `IsTestProject` was not used because it is set by the test package
   targets (inferred, not checked), and the explicit marker is symmetric with Plugin. Update SPEC 4.2 if adopted.
3. FakeItEasy version: under central package management, `Dvx.targets` adds a `PackageVersion` item plus a
   version-less `PackageReference` (works, confirmed). A consumer with central package management OFF would hit
   NU1008-style or missing-version errors in the other direction (version-less reference). Task 18 should either
   require central package management in the consumer template (matches the dvx repo itself) or add
   `Version` conditioned on `'$(ManagePackageVersionsCentrally)' != 'true'`. Untested.
4. The analyzer project needs `NoWarn RS2008` (or release-tracking files) under `TreatWarningsAsErrors` with
   `EnforceExtendedAnalyzerRules`; the real `Dvx.Analyzers` will hit this.
5. A consumer with `TreatWarningsAsErrors=true` turns every Dvx analyzer warning into a build break; this is
   intended for the real rule set but the template should say so.
