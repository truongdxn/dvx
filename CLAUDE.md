# CLAUDE.md — dvx

dvx is internal tooling for Microsoft Dataverse plugin development: a Plugin SDK (attributes + base
classes, vendored as source), Roslyn analyzers, a CLI (`dvx`) and a Visual Studio 2026 extension.
**SPEC.md is the source of truth.** Read it before any task. If something is not in SPEC.md, ask — do
not assume. Decisions and their history are in SPEC Appendix B.

## The one guarantee

Plugin assembly, type, step, image and Custom API IDs must be **identical** in the dev environment and
in the solution CI builds. Any change that could change the ID of an unchanged identity is a bug.

## Non-negotiables

1. **IDs are computed, never stored.** UUID v5 over the frozen namespace in `Dvx.Core` and the
   identity keys in SPEC §7. The namespace, key formats and normalization never change once M2 ships.
   Golden tests are never edited to make a test pass. There is no lock file.
2. **Changing an identity is not a rotation.** A new message/table/stage, class name or assembly name
   creates a new component by design (SPEC §7).
3. **Never invent solution XML** (SP2). Structure comes from `tests/fixtures/`; values come from
   fixtures or Dataverse metadata, never from guesses. If a structural case is not covered, stop and
   ask for a fixture.
4. **Never load plugin assemblies for execution.** `MetadataLoadContext` only.
5. **All logic in `Dvx.Core`.** CLI = argument parsing. VSIX = UI that runs `dvx --json` as a child
   process; it never references `Dvx.Core` or the Dataverse client. Every workflow works from the CLI.
6. **Plugin SDK is a shared project, C# 7.3, net462, stateless.** Only immutable reflection caches in
   thread-safe collections.
7. **Only `dvx deploy` creates, changes or deletes registration components.** The Dataverse MCP server
   is read-only in this workflow.
8. **The dev environment is shared.** Never touch assemblies that are not in code. Deletes, foreign
   replacements, mode changes and recreates always need confirmation (prompt or explicit flag); AI
   agents never pass those flags.
9. **Respect Dataverse immutability** of Custom API fields (SPEC §7).
10. **Deploy only to allowlisted environments**, checked before any network call.
11. **No secrets, tokens or record data** in files, logs, output or test data. Credentials live only in
    the OS secure store.
12. **Dependencies:** only the allowlist in SPEC §11. Anything else requires asking first.
13. **Vendored content** (`build/dvx/`) is written only by `dvx init` / `dvx sdk sync` and is
    checksum-verified.
14. **Comments and identifiers in English.**

## Tech baseline

- `Dvx.Core`, `Dvx.Cli`: net8.0
- `Dvx.Contracts`: netstandard2.0
- `Dvx.Analyzers`: netstandard2.0, `Microsoft.CodeAnalysis.CSharp` 4.8.x
- `Dvx.Plugin.Sdk`, `Dvx.Testing`: shared projects, net462; testing uses an in-memory
  `IOrganizationService` plus FakeItEasy
- `Dvx.Vsix`: Visual Studio 2026 (model decided by Spike D)
- SDK, testing, analyzers and templates are embedded in `Dvx.Cli`, the only NuGet package dvx produces
- Tests: MSTest everywhere; analyzer tests compile snippets with `CSharpCompilation`
- Central package management (`Directory.Packages.props`), nullable enabled outside the SDK, warnings
  as errors

## Working rules

- One milestone (SPEC §13.4) per session. End each with green `dotnet build` and `dotnet test`, then
  stop and summarize. Do not start the next milestone unprompted. Respect milestone gates.
- Plan first: list the files you will create/change and the tests you will add.
- Tests before or with code. Every verify and analyzer rule has a passing and a failing test. Every CLI
  command has JSON and exit-code snapshot tests.
- Spikes produce `docs/spikes/<name>.md`: question, method, result, decision, confirmed vs inferred.
  Spike code is throwaway.
- `tests/fixtures/` is read-only.
- When a Dataverse behavior is uncertain, say so and propose a spike rather than coding a guess.
- Keep this file's Status section current at the end of each session.

## Commands

```
dotnet build
dotnet test
# net462 SDK tests on Linux (needs mono: apt-get install mono-runtime mono-devel)
dotnet test tests/Dvx.Plugin.Sdk.Tests -p:IsTestProject=true
```

(Extend this section as CLI commands become available.)

## Status

- Design: SPEC v2 — SP0 + SP1 detailed, SP2–SP4 outlined; awaiting review
- Current milestone: next is M2 (`AttributeReader`, `Identity`, `DesiredState`, `describe`); gate M1
  met
- Completed: M1 — SDK attributes + runtime base classes, `Dvx.Testing` (41 net462 tests green on
  mono and on Windows, user-confirmed)
- Repo `nuget.config` clears machine feeds and maps every package to nuget.org (NU1507 fix for
  machines with extra feeds)
  - Identity attribute properties (message, entity, stage, mode, unique names, field types) have
    private setters: set only through constructor arguments
  - `InMemoryOrganizationService` INFERRED behaviors to confirm in Spike C: case-insensitive string
    equality, NotEqual excludes nulls; also open: `ParameterCollection` key casing, null output values
- Completed: M0 — repo scaffold (`dotnet build` 0 warnings; `dotnet test` green, incl. Windows)
- Completed: S-A — go, attribute shape unchanged (`docs/spikes/attribute-read.md`)
- Completed: S-F — go; Linux `dotnet build` plus user-run VS 2026, CI image and net462 test checks
  (`docs/spikes/vendoring.md`)
- Approved after S-F: SPEC D30 (`DvxProjectKind=Test`), D31 (analyzer release-tracking files)
- Spike code lives in `spikes/` (throwaway, not in `dvx.sln`)
- net462 `LangVersion` 7.3 / `Nullable` disable live in `Directory.Build.targets` (a TargetFramework
  condition in `.props` never matches)
- M0 notes:
  - `Dvx.Plugin.Sdk.Tests` (net462) builds everywhere but runs only on Windows (`IsTestProject` false
    elsewhere). On non-Windows, net462 builds use the SDK's implicit
    `Microsoft.NETFramework.ReferenceAssemblies` (approved, build-only).
  - `.shproj` files are not in `dvx.sln` (`dotnet sln add` rejects them outside Visual Studio); the
    `.projitems` imports compile their content. Add them from VS on Windows if wanted.
  - Placeholder tests use `Assert.IsNotNull(...)`: `Assert.IsTrue(true)` fails MSTEST0032 under
    warnings as errors.
  - Not yet added: `packages.lock.json` / locked restore, VSIX packages (Spike D).
- Decided before M2: SPEC D32 (Spike C scope extended), D33 (identity frozen as SPEC §7)
- Open questions: SPEC §15
