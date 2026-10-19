# dvx — Dataverse Plugin Tooling Specification

Status: **v2 draft — SP0 + SP1 detailed; SP2–SP4 outlined.** Source of truth for all implementation
work. Anything not covered here must be raised as a question, not assumed. Decisions and their
history are in Appendix B.

---

## 1. Purpose

Replace the current plugin workflow:

> write code → register via Plugin Registration Tool (PRT) → test on dev → register again into the
> repo's solution XML with a separate WinForms tool

with:

> write code (attributes describe registration) → unit test → build → deploy to the shared dev
> environment from Visual Studio or the CLI → test on dev → merge request → CI builds and packs the
> Power Apps solution

**Core guarantee:** plugin assembly, plugin type, step, image and Custom API IDs are identical in the
dev environment and in the solution built by CI. Every design decision serves this guarantee.

**Success criterion:** a developer, or GitHub Copilot in agent mode, completes
new plugin → dev deploy → test → merge request → CI pack with matching IDs and without PRT.

The workflow must work the same whether code is written by a developer or by an AI agent using the
Dataverse MCP server (section 14.4).

---

## 2. Scope and sub-projects

dvx is built as five sub-projects, each with its own spec → plan → implementation cycle (D21).

| Sub-project | Content | Status in this spec |
|---|---|---|
| SP0 | Spikes A, B, C, D, F (go/no-go) | Detailed (section 13) |
| SP1 | Core dev loop: SDK, testing helpers, analyzers, IDs, `describe` / `verify` / `deploy`, auth, vendoring, templates, `trace tail` | Detailed (sections 3–13) |
| SP2 | CI solution generation, pack, baseline check | Outline (section 14.1) |
| SP3 | VSIX for Visual Studio 2026 | Outline (section 14.2) |
| SP4 | AI skills and agent files | Outline (section 14.3) |

Out of scope for all of v1:

- Existing plugin objects: dvx is **greenfield**. No import, migration or adoption of existing
  registrations; assemblies not managed by dvx are never touched (D16).
- Custom workflow activities, plugin packages (`pluginpackage`), steps on Custom API messages (D6),
  impersonation, secure configuration in code, entity code generation (D18).

---

## 3. Environment model and constraints

- **One shared dev environment** per customer project, used by the whole team. This is a company and
  customer rule and cannot be changed (D8). All deploy logic assumes other branches deploy to the same
  environment concurrently.
- Test and production environments are reached only through pipeline solution import. dvx never
  deploys to them.
- Customers provide a client ID + client secret + tenant ID for deployment; dvx uses only these
  (D25). The shared Administrator account is not used by dvx.
- The organization-level Plug-in trace log setting belongs to the customer; dvx never changes it (D27).
- dvx development itself (SP0 spikes, integration tests) needs a **scratch environment in the company
  tenant**, where registrations can be created and deleted freely. Customer environments are never
  used for that.

---

## 4. Consumer repository model

### 4.1 Solutions

- One C# solution (`.sln`) corresponds to exactly one Power Apps solution, described by `dvx.json`.
- Each Power Apps solution has one `kind`:
  - `plugin` — assemblies, plugin types, steps, images
  - `customapi` — assemblies, plugin types, Custom APIs, request parameters, response properties
- These solutions contain only plugin components; CI generates them completely (SP2). The repo does
  not store unpacked solution XML.
- Every assembly belongs to exactly one `dvx.json`. Custom API handler classes live in the Custom
  API solution's own assembly.
- Code shared between assemblies is shared as **source** (shared projects), never as a DLL, because
  classic plugin assemblies cannot have dependent assemblies.

### 4.2 Projects

- Plugin and test projects are **SDK-style** csproj targeting `net462`.
- A plugin project references only `Microsoft.CrmSdk.CoreAssemblies` (compile-time) and shared
  projects. Anything else is a verify error.
- Plugin projects carry `<DvxProjectKind>Plugin</DvxProjectKind>` (written by `dvx new`).

### 4.3 Layout

```
contoso-d365/
  .config/dotnet-tools.json   # pins the dvx version (single version source)
  nuget.config                 # nuget.org + dvx tool feed, package source mapping
  dvx.repo.json                # environment allowlist (committed)
  dvx.user.json                # selected environment (git-ignored)
  Directory.Build.targets      # generated; imports build/dvx/Dvx.targets
  build/dvx/                   # vendored by dvx sdk sync; read-only
    Dvx.Plugin.Sdk/            # shared project
    Dvx.Testing/               # shared project
    Dvx.targets
    analyzers/Dvx.Analyzers.dll
    dvx.sdk.lock
  .dvx/logs/                   # command summaries, JSONL (git-ignored)
  tools/packages/              # phase 1: Dvx.Cli .nupkg only
  src/
    Shared/Contoso.Model/      # shared project: entity constants (section 6.6)
    Plugins/
      Contoso.Plugins.sln
      dvx.json
      Contoso.Plugins/
      Contoso.Plugins.Tests/
    CustomApis/
      Contoso.CustomApis.sln
      dvx.json
      Contoso.CustomApis/
      Contoso.CustomApis.Tests/
```

There is **no lock file** (D22). IDs are computed from code (section 7).

### 4.4 Configuration files

`dvx.repo.json` (committed) — environment allowlist; contains no client IDs, tenant IDs or secrets:

```json
{
  "environments": {
    "dev": { "url": "https://contoso-dev.crm.dynamics.com", "shared": true }
  }
}
```

`dvx.user.json` (git-ignored) — selected environment and optional personal environments, merged into
the allowlist for that user only.

`dvx.json` (next to each `.sln`, committed):

```json
{
  "kind": "plugin",
  "solution": {
    "uniqueName": "ContosoPlugins",
    "displayName": "Contoso Plugins",
    "publisher": { "uniqueName": "contoso", "prefix": "contoso" },
    "version": { "major": 1, "minor": 0 },
    "packageType": "managed"
  },
  "projects": ["Contoso.Plugins/Contoso.Plugins.csproj"]
}
```

Config discovery: `dvx.json` at the `.sln` directory; walk up for `dvx.repo.json`.

### 4.5 Vendored SDK (`build/dvx/`)

- Written only by `dvx init` and `dvx sdk sync`; always matches the CLI version pinned in
  `.config/dotnet-tools.json`. Upgrade = change the version, `dotnet tool restore`, `dvx sdk sync`.
- `dvx.sdk.lock` records the dvx version and the SHA-256 of every vendored file; `verify` fails on any
  difference.
- `Dvx.Analyzers.dll` is the only committed binary (accepted exception, checksum-verified).
- `Dvx.targets` (imported by the generated `Directory.Build.targets`):
  - adds `<Analyzer Include="build/dvx/analyzers/Dvx.Analyzers.dll" />` to plugin and test projects
  - adds `<InternalsVisibleTo Include="$(AssemblyName).Tests" />` to plugin projects
  - adds the FakeItEasy package reference to test projects
  - runs no dvx command during build (there is no lock file to sync)

---

## 5. Components

### 5.1 Deliverables

| Project | Target | Distribution | Purpose |
|---|---|---|---|
| `Dvx.Plugin.Sdk` | shared project, C# 7.3, net462 | Embedded in CLI; vendored | Attributes, base classes, `DvxSdkInfo` |
| `Dvx.Testing` | shared project, net462 | Embedded in CLI; vendored | `PluginTestContext`, in-memory service, FakeItEasy helpers |
| `Dvx.Analyzers` | netstandard2.0, Roslyn 4.8 | Embedded in CLI; vendored binary | Compile-time rules |
| `Dvx.Contracts` | netstandard2.0 | internal | JSON DTOs: describe, plan, deploy result, diagnostics |
| `Dvx.Core` | net8.0 | internal | All logic |
| `Dvx.Cli` | net8.0 | dotnet tool `dvx` — the only dvx NuGet package | Thin command layer |
| `Dvx.Vsix` | VS 2026 | `.vsix` (SP3) | Thin GUI that runs `dvx` |

### 5.2 `Dvx.Core` modules

| Module | Responsibility |
|---|---|
| `AttributeReader` | Read attributes from a built DLL via `MetadataLoadContext` |
| `Identity` | Compute UUID v5 IDs from identity keys (section 7) |
| `DesiredState` | Build the desired registration from attributes + IDs |
| `Verifier` | Offline and online rules (section 10) |
| `EnvReader` | Read registrations and metadata from the environment |
| `Planner` | Diff desired vs environment; classify changes (section 9) |
| `Applier` | Execute a plan in the mandatory order |
| `GitStamp` | Branch, commit, user for the assembly stamp (D26) |
| `SecretStore` | Encrypted per-user credential storage (D25) |
| `Vendoring` | `init` / `sdk sync` / `sdk check` |
| `Templates` | `dvx new` project and item templates |
| `TraceReader` | Read plug-in trace logs for `trace tail` |

### 5.3 Architectural rules

- All logic lives in `Dvx.Core`. The CLI parses arguments; the VSIX renders UI.
- The CLI is complete on its own; every workflow works from a terminal.
- The VSIX never loads `Dvx.Core` or the Dataverse client in-process; it runs `dvx` as a child process
  with `--json`.
- Plugin assemblies are inspected, never executed: `MetadataLoadContext` only.
- With `--json`: results on stdout in the `Dvx.Contracts` schema, progress on stderr, no prompts.

---

## 6. Plugin SDK

Shared project compiled into the consumer's plugin assembly. C# 7.3, net462, all types `internal`.
Contains `internal static class DvxSdkInfo { public const string Version = "x.y.z"; }`.

### 6.1 Registration rules

- One entity plugin class = exactly one step. Flows may share a class only if message, table, stage,
  mode and filtering attributes are identical.
- Any SDK message may be used for steps in dev. Release packing support per message is SP2 (D15).
- At most one pre-image and one post-image per step; aliases fixed: `PreImage`, `PostImage`.
- Image attributes: `null` = no image; empty array = error; all columns only via
  `ImageAttributes.All` (warning) (D4).
- Update steps require `FilteringAttributes`; opting out only via `FilteringAttributes.All`
  (warning) (D5).
- Isolation: always Sandbox. Impersonation: not supported. Supported deployment: server only.
- Secure configuration is never declared in code; secrets come from environment variables
  (Key Vault backed).
- Step name: format decided in a separate naming session (D7). Until then: `{TypeFullName}: {Message}
  of {entity}`.

### 6.2 Attributes

```csharp
[PluginStep("Update", Account.EntityLogicalName, Stage.PreOperation, Mode.Synchronous,
    FilteringAttributes = new[] { Account.Fields.Name, Account.Fields.CreditLimit },
    PreImageAttributes = new[] { Account.Fields.Name, Account.Fields.CreditLimit },
    Rank = 1,
    Description = "Validate account changes")]
internal sealed class AccountUpdatePreOperation : EntityPluginBase { /* ... */ }
```

`PluginStepAttribute`: message, primary entity, `Stage` (PreValidation=10, PreOperation=20,
PostOperation=40), `Mode` (Synchronous=0, Asynchronous=1), `FilteringAttributes`,
`PreImageAttributes`, `PostImageAttributes`, `Rank` (default 1), `Description`, `AsyncAutoDelete`,
`UnsecureConfiguration`. Markers: `FilteringAttributes.All`, `ImageAttributes.All`.

Custom API:

```csharp
[CustomApi("contoso_CalculateDiscount",
    BindingType = CustomApiBindingType.Global,
    IsFunction = false,
    IsPrivate = false,
    AllowedCustomProcessingStepType = CustomProcessingStepType.None,
    ExecutePrivilegeName = "prvContosoCalculateDiscount",
    DisplayName = "Calculate Discount")]
internal sealed class CalculateDiscountApi
    : CustomApiBase<CalculateDiscountRequest, CalculateDiscountResponse> { }

internal sealed class CalculateDiscountRequest
{
    [CustomApiRequestParameter("contoso_Amount", CustomApiFieldType.Money)]
    public Money Amount { get; set; }

    [CustomApiRequestParameter("contoso_CustomerId", CustomApiFieldType.EntityReference, IsOptional = true)]
    public EntityReference Customer { get; set; }
}

internal sealed class CalculateDiscountResponse
{
    [CustomApiResponseProperty("contoso_Discount", CustomApiFieldType.Decimal)]
    public decimal Discount { get; set; }
}
```

- `CustomApiBindingType`: `Global`, `Entity`, `EntityCollection`. `Entity` / `EntityCollection`
  require `BoundEntityLogicalName`; `Global` forbids it.
- Response properties have no `IsOptional`.

### 6.3 Base classes

- `PluginRuntime` — created per `Execute` call, never stored on the instance. Exposes
  `PluginContext`, `Tracing`, lazily created `UserService` and `SystemService`, `Target`, `PreImage`,
  `PostImage` (fixed aliases) and `Shared` (per-execution dictionary).
- `PluginBase : IPlugin` — builds `PluginRuntime`, traces entry, re-throws
  `InvalidPluginExecutionException` unchanged, wraps other exceptions, calls `ExecuteCore`.
- `EntityPluginBase : PluginBase` — `protected abstract IEnumerable<FlowBase> CreateFlows(PluginRuntime)`
  (explicit ordered list); runs each flow whose `CanExecute()` is true, tracing flow name before and
  after. Depth guard `protected virtual int? MaxDepth => null;` — disabled unless a class opts in (D9).
- `FlowBase` — constructor takes `PluginRuntime`; `virtual bool CanExecute() => true` (business
  conditions only); `abstract void Execute()`. A flow may be reused by several plugin classes.
- `CustomApiBase<TRequest, TResponse> : PluginBase` — maps `InputParameters` → `TRequest` and
  `TResponse` → `OutputParameters` by parameter attributes;
  `protected abstract TResponse Handle(TRequest request, PluginRuntime runtime)`.

### 6.4 Statelessness

Plugin instances are cached and reused concurrently. No mutable instance or static state in the SDK or
consumer plugin code. Only immutable reflection caches in thread-safe collections are allowed.

### 6.5 Testing helpers (`Dvx.Testing`)

- `PluginTestContext` builds the service provider, execution context, tracing and images for a
  plugin under test.
- An in-memory `IOrganizationService` supports Create, Retrieve, Update, Delete and RetrieveMultiple
  with basic `QueryExpression`. Tests seed data, run the plugin and assert on resulting data.
- FakeItEasy covers everything else: special messages, failure injection, call verification (D29).

### 6.6 Entity constants (no code generation, D18)

- Written by developers or by the AI skill (SP4), which reads only the needed columns through the
  Dataverse MCP server.
- Rules: one file per table in the shared model project; `public const string` only (never
  `static readonly`); `EntityLogicalName`, `PrimaryIdAttribute`, `PrimaryNameAttribute`; columns in a
  nested `Fields` class; only columns used by code.
- Checked against environment metadata during `deploy --plan` and by `dvx verify --online` (D24).

---

## 7. Identity and IDs

There is no lock file. Every ID is computed on each build (dev, deploy, CI) (D22):

```
id = UUIDv5(namespace, key)
```

The namespace GUID is a constant in `Dvx.Core` and never changes.

| Component | Key |
|---|---|
| Assembly | `asm:{AssemblyName}` |
| Plugin type | `type:{AssemblyName}/{TypeFullName}` |
| Step | `step:{AssemblyName}/{TypeFullName}\|{message}\|{entity}\|{stage}` |
| Image | `img:{stepKey}\|pre` or `img:{stepKey}\|post` |
| Custom API | `api:{uniquename}` |
| Request parameter | `apireq:{api}\|{param}` |
| Response property | `apiresp:{api}\|{property}` |

Normalization:

- `message`, `entity` and all unique names: lowercase invariant (Dataverse is case-insensitive).
- `AssemblyName` and `TypeFullName`: exact, ordinal (.NET is case-sensitive).
- `stage`: numeric (10 / 20 / 40).

Not part of identity (updated in place, ID kept): mode (D10), rank, filtering attributes, description,
configuration, image columns.

Consequences (accepted):

- Renaming a class, namespace or assembly creates new components; old ones are deleted.
- Changing a step's message, table or stage creates a new step and images; changing back yields the
  original IDs again (D2).

Assembly identity in Dataverse is name + major.minor + culture + public key token. An environment
assembly with the same name but different major.minor or token is a plan error. Only build and
revision numbers may change.

Safeguards:

- Golden tests in the dvx repo: fixed identity keys with expected GUIDs. Never edited to make a test
  pass.
- Duplicate IDs within one DLL are an internal error (exit 9).
- CI compares `describe` output against the baseline of the last release (SP2).

Custom API immutable fields (unique name, binding type, bound entity, is function, allowed custom
processing step type; for parameters: unique name, type, is optional): Dataverse does not allow them
to change. In dev, deploy deletes and recreates the component with the same ID after confirmation;
CI blocks the change against the baseline (D1, SP2).

---

## 8. CLI commands (SP1)

| Command | Purpose |
|---|---|
| `dvx init` | Repo setup: tool manifest entry, `nuget.config`, `dvx.repo.json`, `Directory.Build.targets`, `build/dvx/`, shared model project, `.gitignore` entries |
| `dvx sdk sync` / `sdk check` | Vendor SDK, testing helpers, analyzers and targets for the CLI version / verify checksums |
| `dvx auth set --env <n>` / `auth status` | Store credentials (section 11) / show masked status and test the connection |
| `dvx env list` / `env select <n>` | Allowlist and selection |
| `dvx new solution` | Power Apps solution on dev + C# solution, `dvx.json`, projects, strong-name key, test project |
| `dvx new plugin\|customapi --name <n> [options]` | Correctly shaped classes plus a test class |
| `dvx describe --project <csproj> --json` | Desired registration with identity keys and IDs |
| `dvx verify [--all] [--online]` | Rules in section 10 |
| `dvx deploy --project <csproj> \| --all --plan [--json]` | Compute the plan (no changes) |
| `dvx deploy --apply --plan-file <f> [--allow-deletes] [--replace-foreign] [--allow-mode-change] [--allow-recreate]` | Apply exactly the previewed plan |
| `dvx trace tail --project <csproj> [--since <t>] --json` | Plug-in trace logs for the project's plugin types |

`deploy`, `verify --online`, `trace` and `new solution` check the allowlist before any network call.

---

## 9. Deploy on the shared dev environment

### 9.1 Plan

1. Build the project and read the DLL. Stop if `DvxSdkInfo.Version` differs from the CLI version.
2. Build the desired state and run offline verify; errors stop the plan.
3. Read the environment: the assembly by name with its plugin types, steps, images, Custom APIs and
   parameters; `sdkmessage` / `sdkmessagefilter`; table and column metadata for entity constants;
   referenced privileges. Run online verify.
4. Diff and classify every change:

| Class | Example | Confirmation |
|---|---|---|
| Own change | create / update | none |
| Own delete | class removed from code | required |
| Foreign (D23) | step or type only in the environment, or a different trigger for the same plugin type, in an assembly present in code | required ("replace?"); declining skips the whole assembly |
| Mode change (D10) | Sync → Async | required |
| Recreate (D1) | Custom API immutable field changed | required |
| Info | another (non-dvx) assembly registers on the same message and table | none, display only |

5. Output the plan (text or JSON) with a hash of the environment state read in step 3.

Foreign components are shown with the stamp of the deploy that created them (section 9.3).

Replacing a foreign plugin type is required for the assembly upload to succeed: Dataverse rejects an
assembly update while a registered type is missing from the new DLL.

### 9.2 Apply

- Terminal: prompts per class. Non-interactive (`--json`, VSIX): each class requires its flag:
  `--allow-deletes`, `--replace-foreign`, `--allow-mode-change`, `--allow-recreate`; otherwise exit 6
  and nothing is applied for that assembly. The VSIX shows a dialog and passes the flags the user
  confirmed. AI agents never pass these flags.
- Before applying, the environment hash must match the plan; otherwise exit 5.
- Mandatory order:
  1. Delete, inner → outer: images, steps, Custom API parameters/properties, Custom APIs, plugin
     types.
  2. Upload assembly content and write the stamp.
  3. Create / update, outer → inner: plugin types, steps, images, Custom APIs,
     parameters/properties. Every create uses the computed ID and the `MSCRM.SolutionUniqueName`
     header for the solution in `dvx.json`.
- On failure: stop at the failing request and report applied, pending and the failing request with
  Dataverse's message. Re-running `deploy` converges because IDs are deterministic. Transaction use
  depends on Spike C.

### 9.3 Stamp

The assembly description is set to
`dvx: {branch} @ {shortCommit} · {git user.name} <{git user.email}> · {UTC time}` (D26). Without git:
`unknown`, with a warning.

### 9.4 `deploy --all`

- Every assembly in every `dvx.json` is planned and applied independently; a skipped assembly does not
  affect the others.
- Assemblies in the environment that are not in code are never touched (D16, D23).
- A summary lists synced, skipped (with reason) and failed assemblies; partial success is exit 7.

---

## 10. Verification

| Layer | Runs | Catches |
|---|---|---|
| Analyzers (Roslyn 4.8, D28) | While typing and on build: VS, VS Code, CI | What source code shows |
| `verify` offline | `dvx verify`, `deploy --plan`, CI | What needs the DLL or config |
| `verify --online` | `deploy --plan`, `dvx verify --online` | What needs the environment (D24) |

Analyzer errors:

- Entity plugin class without exactly one `[PluginStep]`
- Pre-image on Create, post-image on Delete, post-image outside PostOperation
- Image with an empty attribute array
- Asynchronous mode outside PostOperation; `AsyncAutoDelete` on a synchronous step
- `FilteringAttributes` on a message other than Update; Update without `FilteringAttributes`
- Custom API without `ExecutePrivilegeName`; binding inconsistent with `BoundEntityLogicalName`;
  function without a response property
- Mutable instance or static fields in classes deriving from `PluginBase` or `FlowBase`

Analyzer warnings: `ImageAttributes.All`, `FilteringAttributes.All`, `new ColumnSet(true)`, string
literal where an entity constant exists, `FlowBase` class not used by any plugin.

Offline verify errors (in addition to re-checking every analyzer rule, in case analyzers did not
load):

- `DvxSdkInfo.Version` differs from the CLI; vendored files differ from `dvx.sdk.lock`
- Custom API unique name without the publisher prefix from `dvx.json`
- Component kind not allowed by `dvx.json` `kind`; assembly in more than one `dvx.json`
- Plugin project reference other than `Microsoft.CrmSdk.CoreAssemblies` and shared projects
- Step on a Custom API message (D6)
- Duplicate IDs within one DLL (internal error)

Online verify errors:

- Table or column in entity constants not found
- Message not found, or not available for the table (`sdkmessagefilter`)
- `ExecutePrivilegeName` not found
- Environment assembly with the same name but different major.minor or public key token

Diagnostics share one schema: `code` (`DVX0nn`), `severity`, `message`, `file`, `line`.

---

## 11. Authentication, configuration security, dependencies

- `dvx auth set --env <n>` prompts for client ID, tenant ID and secret (hidden input) and stores all
  three encrypted per user (Windows DPAPI) through the storage helper of
  `Microsoft.Identity.Client.Extensions.Msal`, key `dvx:{envUrl}` (D25).
- `dvx auth status` shows environment, masked client ID, tenant and a connection test; never the
  secret.
- Connections use `ServiceClient` with a client-secret connection string built in memory only.
- 401 from Dataverse: "secret expired or invalid, run `dvx auth set --env <n>`", exit 3.
- Allowlist checked before any network call (exit 4). Network calls only to the selected Dataverse
  environment and Microsoft identity endpoints. No telemetry.
- Never written to files, logs or output: secrets, tokens, record data. `trace tail` prints only the
  text written by plugins.
- CI in SP1 is offline: build, tests, analyzers, `verify`. No credentials.

Dependency allowlist (anything else requires explicit approval):

- `Microsoft.CrmSdk.CoreAssemblies`, `Microsoft.PowerPlatform.Dataverse.Client`
- `Microsoft.Identity.Client.Extensions.Msal` (secret storage)
- `System.Reflection.MetadataLoadContext`, `System.CommandLine`
- `Microsoft.CodeAnalysis.CSharp` 4.8.x — `Dvx.Analyzers` only
- `MSTest` (all test projects), `FakeItEasy` (`Dvx.Testing` and consumer test projects; approved, D29)
- `Microsoft.VisualStudio.Extensibility` or `Microsoft.VisualStudio.SDK` (VSIX, decided by Spike D)

`packages.lock.json` with locked restore in CI; NuGet package source mapping (`Dvx.Cli` → dvx feed,
everything else → nuget.org).

---

## 12. Errors, exit codes, logs

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | Diagnostic errors (build, analyzer, verify, unreadable DLL) |
| 2 | Usage error |
| 3 | Credentials missing or expired |
| 4 | Environment not in the allowlist |
| 5 | Environment changed since the plan |
| 6 | Confirmation required in non-interactive mode |
| 7 | `deploy --all` partially succeeded |
| 8 | Dataverse or network error after retries |
| 9 | Internal dvx error |

- Throttling (service protection, 429): wait for `Retry-After`, up to 5 retries, then exit 8.
- Ctrl+C during apply: finish the current request, stop, report like a failure.
- Build failure: MSBuild output passed through, exit 1. Missing reference assemblies for
  `MetadataLoadContext`: diagnostic, exit 1.
- Deploy result schema:
  `{ assemblies: [{ name, status, applied[], pending[], failedAt?, reason? }] }`.
- Every command appends one summary line to `.dvx/logs/<date>.jsonl` (git-ignored): command,
  arguments without values that could be secrets, exit code, diagnostic codes, duration. No secrets,
  tokens or record data. The SP4 diagnostics skill reads these files.

---

## 13. Testing and spikes

### 13.1 SP0 spikes

Each spike is time-boxed (1–2 days) and ends with `docs/spikes/<name>.md`: question, method, result,
decision, and what is confirmed vs inferred. Spikes B and C use the scratch environment (section 3).

| Spike | Question | If no-go |
|---|---|---|
| A | Can `MetadataLoadContext` read `internal` attributes compiled from the shared project, including `string[]`, enums and `DvxSdkInfo.Version`? | Change how attributes are declared |
| B | Structural fixtures (Appendix A); is step/image XML structure identical across messages; can a managed package be packed from generated source; step name format | Per-message fixtures (D15 fallback); or import into a build environment and export managed |
| C | Client-supplied IDs for all component tables; transactions; in-place mode update; solution header; two assemblies with the same name and different versions; minimum privileges of the application user | Adjust section 9 |
| D | VSIX model on VS 2026 (out-of-process Extensibility vs VSSDK): toolbar, tool window, dialogs, child process launch, coexistence with Copilot | Use the other model |
| F | Vendoring: `Directory.Build.targets` import, analyzer loaded by path, `InternalsVisibleTo`, Roslyn 4.8 on VS 2026 and the real CI image | Change analyzer distribution |

### 13.2 SP1 tests

- Unit (MSTest): v5 golden tests; `AttributeReader` against compiled sample projects; `Planner`
  against fake environment snapshots covering every change class and skipped assemblies; a passing
  and a failing test for every verify rule; SDK concurrency tests (one plugin instance, parallel
  executions).
- Analyzers: compile code snippets with `CSharpCompilation` (no Roslyn testing package).
- Contract: snapshot tests of JSON output and exit codes for every command.
- Integration (opt-in, scratch environment): deploy create / update / delete / foreign / mode change;
  re-run gives an empty plan; interrupted apply converges on re-run.

### 13.3 SP1 done

- `dotnet build` and `dotnet test` green.
- End to end on the scratch environment: `dvx new plugin` → build → `deploy` → environment IDs equal
  `describe` → deploying again gives an empty plan.

### 13.4 Milestones (SP0 + SP1)

Each milestone ends with green build and tests and a commit. One milestone per session.

| # | Milestone | Gate |
|---|---|---|
| M0 | Repo scaffold: projects, `Directory.Build.props`, central package management, empty tests | — |
| S-A, S-F | Spikes A and F | before M1 |
| S-C | Spike C | before M5 |
| S-B, S-D | Spikes B and D | before SP2 / SP3 start |
| M1 | SDK + `Dvx.Testing` | S-A, S-F |
| M2 | `AttributeReader`, `Identity`, `DesiredState`, `describe` | M1 |
| M3 | Verify offline + analyzers | M2 |
| M4 | Auth, environments, `EnvReader`, online verify | M2 |
| M5 | Deploy plan/apply, `--all`, stamp | M3, M4, S-C |
| M6 | `init`, `sdk sync/check`, `new`, `trace tail`, JSONL logs; end-to-end done criteria | M5 |

---

## 14. Later sub-projects (outline)

### 14.1 SP2 — CI solution generation

- CI generates the complete unpacked solution and packs it (managed per `dvx.json`), offline.
- Generic step template (D15): XML structure from structural fixtures; per-message values from
  metadata; each message + image combination confirmed by a Dataverse round trip; unconfirmed
  combinations block `pack --release`.
- Baseline: `describe` snapshot of the last release; CI fails when an existing identity's ID changes or
  a Custom API immutable field changes.
- Release pipeline imports in upgrade mode, in dependency order; nightly upgrade test including
  removal of a plugin class.
- Open: where XML is generated and where message values live (Q7, options A/C/D in Appendix B), image
  property for unused messages, catalog format, XML diff on merge requests, local preview, baseline
  storage.

### 14.2 SP3 — VSIX (Visual Studio 2026)

- Required (D17). Toolbar: environment selector, auth status, Verify. Deploy from project context
  menu with a plan dialog that passes confirmation flags. Error List integration.
- Trace tool window (D19): filtered to the open solution's plugin types, refresh after deploy, jump to
  class and flow, "ask Copilot" with exception, stack and related code; banner when the trace setting
  is Off or Exception (D27).
- All data through `dvx … --json`.

### 14.3 SP4 — AI skills and agent files

- Plugin development skill (SDK rules, test approach D29, command workflow), entity constants skill
  (MCP read-only, section 6.6), diagnostics skill (reads `.dvx/logs`, build/verify/trace output; every
  recurring error is turned into an analyzer, verify rule or test).
- Agent-neutral markdown content; `dvx init` generates wrappers per agent (Copilot instructions,
  prompts, skills). Snapshot-tested.

### 14.4 AI boundary (applies from SP1)

- Registration components are created, changed and deleted only through `dvx deploy`.
- The Dataverse MCP server is used read-only: schema discovery, test data queries, `plugintracelog`.
- AI agents never edit `build/dvx/**` and never pass `--allow-deletes`, `--replace-foreign`,
  `--allow-mode-change` or `--allow-recreate`.

---

## 15. Open questions

- Step name format and naming conventions (D7, separate session).
- SP2: Q7 and the items in section 14.1.
- SP3 / SP4: diagnostics data model; phase 2 feed for `Dvx.Cli`; `dvx mcp` timing.

---

## Appendix A — Structural fixtures (Spike B, scratch environment)

Register with PRT, export each solution unmanaged and managed, unpack with package type Both, commit
under `tests/fixtures/<case>/` with a README (PRT version, date). Cases must show whether step and
image XML share one structure across messages.

Plugin solution:

1. Create, PreValidation, Sync — no images
2. Update, PreOperation, Sync — filtering attributes + pre-image
3. Update, PostOperation, Async — pre-image + post-image, AsyncAutoDelete
4. Delete, PreOperation, Sync — pre-image
5. Create, PostOperation, Sync — post-image
6. SetState on opportunity, PostOperation — pre-image
7. A message without a primary entity (global)
8. Two steps on the same message/table/stage with different ranks
9. Step with unsecure configuration

Custom API solution:

10. Global action: request parameters of several types (String, Money, EntityReference, optional
    Boolean) and two response properties
11. Entity-bound function with `BoundEntityLogicalName` and `ExecutePrivilegeName`
12. EntityCollection-bound action

Also capture the plugin solution after removing one step, after removing one plugin class, after
changing only the assembly build number, and after changing one step's mode from Sync to Async.

---

## Appendix B — Decision log

Section numbers cited in D1–D29 refer to SPEC v1.1. Struck-through decisions are superseded.

### Decided

- D1 Custom API immutable field change: in dev, deploy may delete + recreate with the same ID
  after confirmation; `verify` blocks it in CI (6.2).
- D2 Step identity = plugin type + message + primary entity + stage; changing any of them creates a
  new step and images with new IDs (6.1).
- D4 All-column images allowed only via `ImageAttributes.All`, with a verify warning (5.1).
- D5 Update without filtering attributes: error by default; explicit `FilteringAttributes.All`
  opt-out with a warning (5.1).
- D6 Steps on Custom API messages: not supported in v1 (2.1).
- D7 Step name format and naming conventions: deferred to a separate session (14).
- D8 The team uses one shared dev environment; foreign-component handling in deploy is mandatory (8.3).
- D9 Depth guard: not mandatory; disabled by default, opt-in per class (5.3).
- D10 Step mode is not part of the identity; a mode change is an in-place update, flagged in the plan
  with its own confirmation `--allow-mode-change` (6.1, 8.2).
- D11 Trigger conflict in the shared dev environment (same plugin type, different trigger from another
  branch): deploy is blocked with exit code 7 and the other branch's stamp; `--take-over` overrides
  (8.3). AI agents never pass `--allow-deletes`, `--allow-mode-change` or `--take-over` (16.3).
  Exit code 7 and `--take-over` are replaced by D23.
- ~~D12 Imported steps follow the same rule as new ones: a trigger change gets a v5 ID; changing back
  does not restore the original real ID (4.5).~~ Superseded by D16.
- ~~D13 Step inventory: read-only `dvx metadata steps --json`; `dvx import` runs it as a pre-check
  report and continues with supported parts (7, 10).~~ Superseded by D16.
- ~~D14 v1 fixtures must support every step message the team uses; no passthrough or legacy-solution
  workaround is needed (supersedes Q5) (5.1, Appendix A).~~ Superseded by D16; message support is
  covered by D15.
- D15 Generic step template: structure from a fixed set of structural fixtures, per-message values
  from metadata/inventory; each message + image combination is confirmed by an automated round trip in
  the scratch environment; unconfirmed combinations work in dev, warn in verify, block
  `pack --release`. Falls back to per-message fixtures if Spike B finds structural differences (5.1).
- D16 Greenfield: dvx targets new plugin projects only. No import/migration of existing plugin
  objects, no imported IDs, no `adopt`. Legacy assemblies in the shared dev environment are never
  touched (dvx only manages assemblies it owns). The shared dev environment (D8) is a fixed company
  and customer rule.
- D17 VSIX is required. Target Visual Studio 2026 (with Copilot agent mode), not VS 2022.
- D18 No entity code generation. An AI skill fetches only the needed columns through the Dataverse MCP
  server and writes entity constants by documented rules (one file per table, `const` only, nested
  `Fields`).
- D19 Plug-in trace logs are viewed in Visual Studio through a VSIX tool window (data via
  `dvx trace tail --json`).
- D20 Process: lock down the most feasible direction, then build the project and trial it with AI
  agents using a plugin-development skill and a diagnostics skill that collects errors for
  improvement.

- D21 Project split into sub-projects, each with its own spec → plan → implementation cycle:
  SP0 spikes A/B/C/D (go/no-go), SP1 core dev loop (SDK + analyzers, v5 IDs, describe/verify/deploy
  on the shared dev environment), SP2 CI solution generation + pack + baseline check, SP3 VSIX for
  VS 2026 (deploy, plan dialog, trace window), SP4 AI skills (plugin development, entity constants,
  diagnostics) and agent files. SP0 + SP1 first. Success: a developer or Copilot completes new plugin →
  dev deploy → test → MR → CI pack with matching IDs and no PRT.

- D22 No lock file. Every ID is UUID v5 computed from code on each build (dev, deploy, CI). ID
  stability and Custom API immutability are checked in CI against a baseline snapshot of the last
  release; before the first release, golden tests of the v5 computation are the safeguard. Sections
  4.5, 5.5, 6 and 9 and CLAUDE.md non-negotiables 2–3 are rewritten in SPEC v2.

- D23 (refines D11) `deploy --all` (and single deploy) on the shared dev environment, from any branch;
  the interactive prompt replaces `--take-over`:
  - Assembly present in code: every difference caused by another branch (trigger conflict, plugin
    types/steps existing only in the environment) is shown as a warning and the user is asked whether
    to replace it. Replacing a foreign plugin type is required for the assembly upload to succeed
    (Dataverse rejects an assembly update while a registered type is missing from the DLL); declining
    skips that assembly.
  - Assembly not present in code (legacy or another branch's new assembly): skipped, never touched.
  - Non-interactive runs (`--json`, AI agents) cannot answer the prompt: exit code 6, nothing applied
    for that assembly.

- D24 Entity constants are checked against environment metadata during `deploy --plan` (invalid
  table or column names block that assembly) and on demand with `dvx verify --online`. CI stays
  offline and does not check them.

- D25 Authentication in SP1: client ID + client secret + tenant ID (provided by the customer for
  deployment) for every online dvx action. `dvx auth set --env <name>` prompts for them and stores
  them encrypted per user in the OS secure store (via `Microsoft.Identity.Client.Extensions.Msal`
  storage); never in files, logs or CLI output. Secret rotation = run `auth set` again. The shared
  Administrator account is not used by dvx. Interactive login is deferred until a customer provides no
  secret.

- D26 The assembly description stamp uses git identity: `branch @ shortCommit · user.name
  <user.email> · time`, read from the local git config.

- D27 dvx never changes the Plug-in trace log setting (organization-level, customer-owned). The trace
  window reads whatever exists and shows a banner with the current level when it is Off or Exception.

- D28 `Dvx.Analyzers` compiles against Roslyn 4.8 (`Microsoft.CodeAnalysis.CSharp` 4.8.x): loads in
  VS 2022 17.8+, VS 2026 and CI images with .NET 8+ SDK. Spike F confirms on VS 2026 and the real CI
  image.

- D29 Testing: `Dvx.Testing` ships an in-memory `IOrganizationService` fake for basic CRUD
  (Create/Retrieve/Update/Delete/RetrieveMultiple with basic QueryExpression); FakeItEasy (MIT,
  approved) covers everything else (special messages, failure injection). The SP4 plugin-development
  skill explains when to use which.

### Asking

- Review of SPEC v2 (SP0 + SP1). Design sections 1–7 approved in chat; scratch environment in the
  company tenant assumed available (section 3).

### To ask

SP1: none.

SP2:
- Q7 Where is solution XML generated (CI vs developer machine; options A/C/D)
- Resolving image `messagepropertyname` for a message no existing step uses (metadata spike)
- Message catalog format: one line per entry
- Generated solution XML diff published on merge requests
- Local solution preview command
- Baseline storage location

SP3 / SP4 / later:
- Diagnostics skill: what is collected, where stored, how findings become rules
- Phase 2 feed for `Dvx.Cli`
- `dvx mcp` timing
