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

1. Analyzer diagnostic (output below is trimmed: paths shortened, summary lines kept), clean `dotnet build Contoso.sln` (0 errors, 2 warnings, no CS8032, no AD0001; also
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

4. Analyzer loaded from the vendored path (raw lines; paths shortened to `.../` for the repo root). csc received
   `/analyzer:.../consumer/build/dvx/analyzers/Dvx.Analyzers.dll`; `dotnet build -getItem:Analyzer` shows
   Identity = that path with `DefiningProjectFullPath` = `build/dvx/Dvx.targets`. The same item appears in the
   test project. The diagnostics in check 1 prove it executed. Raw output:

```
$ dotnet build src/Contoso.Plugins.Tests -getItem:Analyzer | grep -E '"Identity"|DefiningProjectFullPath'
 "Identity": ".../consumer/build/dvx/analyzers/Dvx.Analyzers.dll",
 "DefiningProjectFullPath": ".../consumer/build/dvx/Dvx.targets",
$ dotnet build Contoso.sln -v:d | grep -oE "/analyzer:[^ ]*Dvx[^ ]*" | sort | uniq -c
      2 /analyzer:.../consumer/build/dvx/analyzers/Dvx.Analyzers.dll      (one per project)
$ dotnet build Contoso.sln -v:d | grep -cE "CS8032|AD0001"
0
```

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
| Loading on the VS 2026 host: the csc 4.11 result above does not cover it, because VS runs its own Roslyn host (and its own analyzer loading, e.g. for IntelliSense) | NOT CONFIRMED (same row) |
| Real CI image (`dotnet --info`, build, test, warnings as seen) | NOT CONFIRMED. Steps: spikes/vendoring/README.md |
| FakeItEasy handling in the three consumer cases (CPM without pin, CPM with pin, no CPM), old unconditional targets failing and conditional targets passing | CONFIRMED (Fix round 1 below) |

## Proposals for Task 18 (Dvx.targets shape)

1. Layout: generated `Directory.Build.targets` is only an `<Import Project="$(MSBuildThisFileDirectory)build/dvx/Dvx.targets" />`
   (use `$(MSBuildThisFileDirectory)` inside Dvx.targets for the analyzer path, so it works from any project
   directory). Define `DvxAnalyzerPath` once.
2. Project identification: use the existing `DvxProjectKind` property, with values `Plugin` (SPEC 4.2) and a new
   `Test`. One condition each (`Plugin Or Test` for the analyzer, `Plugin` for IVT, `Test` for FakeItEasy).
   `dvx new` writes the marker for both. `IsTestProject` was not used because it is set by the test package
   targets (inferred, not checked), and the explicit marker is symmetric with Plugin. Update SPEC 4.2 if adopted.
3. FakeItEasy version: a single unconditional `PackageVersion` + version-less `PackageReference` (the first
   spike version) is wrong for two of three consumers. Reproduced in the scratchpad against the first version
   of `Dvx.targets`:

```
(a) CPM consumer that already pins FakeItEasy in its own Directory.Packages.props
    default:                       warning NU1506: Duplicate 'PackageVersion' items found. Remove the duplicate items or use the Update functionality   (build succeeded)
    with TreatWarningsAsErrors:    error NU1506: Warning As Error: Duplicate 'PackageVersion' items found. ...   (Build FAILED)
(b) Consumer without CPM (Version= attributes in its csproj files)
    default:                       warning NU1604: Project dependency FakeItEasy does not contain an inclusive lower bound. Include a lower bound ...   (build succeeded)
    with TreatWarningsAsErrors:    error NU1604: Warning As Error: Project dependency FakeItEasy does not contain an inclusive lower bound. ...   (Build FAILED)
```

   In (a) the vendored pin also silently competes with the consumer's own pin. In (b) the vendored
   `PackageVersion` is ignored because CPM is off. NU1008 is the opposite case (a `Version=` attribute under
   CPM) and would be triggered by a naive "always add Version" fix. `Dvx.targets` therefore handles three cases
   (implemented in the spike file, all three build; NU1506/NU1604 escalated with
   `-p:WarningsAsErrors=NU1506%3BNU1604`, 0 errors, only the 2 intended DVXSPIKE001 warnings):

   | Consumer | Dvx.targets adds |
   |---|---|
   | CPM, no FakeItEasy pin | `PackageVersion FakeItEasy 9.0.1` + version-less `PackageReference` |
   | CPM, consumer already pins it | version-less `PackageReference` only; the consumer's pin wins |
   | No CPM | `PackageReference FakeItEasy Version="9.0.1"` |

   Pin detection is `@(PackageVersion->WithMetadataValue('Identity','FakeItEasy'))` collected in a private item
   (`%(Identity)` in an item condition outside a target is rejected with MSB4190). It relies on
   `Directory.Packages.props` being evaluated before `Dvx.targets`, which holds for the SDK import order.
   In the pinned case the consumer's version may differ from 9.0.1; whether that is acceptable (the Dvx.Testing
   helpers are compiled against 9.0.1) is a Task 18 decision. Not tested: a consumer pin using `VersionOverride`
   on its own reference, and a floating or range pin.
4. The analyzer project needs `NoWarn RS2008` (or release-tracking files) under `TreatWarningsAsErrors` with
   `EnforceExtendedAnalyzerRules`; the real `Dvx.Analyzers` will hit this.
5. A consumer with `TreatWarningsAsErrors=true` turns every Dvx analyzer warning into a build break; this is
   intended for the real rule set but the template should say so.
6. `DvxSpikeNoIvt` in the spike `Dvx.targets` is spike-only (it exists to show the CS0122 negative case). It must
   not ship in the Task 18 file; the shipped `InternalsVisibleTo` item is conditioned on `DvxProjectKind` only.

## Fix round 1

Changes after review: proposal 3 corrected and implemented (three FakeItEasy cases, reproduced failures
above), `DvxSpikeNoIvt` marked spike-only, raw output pasted for the analyzer-path check, and the VS 2026
host row added to the table.
