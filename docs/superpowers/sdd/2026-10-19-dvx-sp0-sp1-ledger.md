# SDD ledger — plan: docs/superpowers/plans/2026-10-19-dvx-sp0-sp1.md

Session scope: Task 0 (M0) only. Spec: SPEC.md (v2).
Environment: Linux container, .NET SDK 8.0.426 installed at /root/.dotnet. No .NET Framework targeting pack, no mono.

## Pre-flight scan (Task 0 only in scope)

| Item | Check | Finding |
|---|---|---|
| Task 0 self-consistency | files created vs steps | consistent: props + sln + empty projects + placeholder tests |
| Task 0 vs Global Constraints (net462 on Linux) | net462 projects need targeting pack | conflict with environment — asked user |
| Task 0 vs SPEC §11 (packages.lock.json, source mapping) | plan silent | see ruling |
| Task 0 "exact versions for every allowlisted package" vs VSIX packages | VSIX model undecided (Spike D) | see ruling |
| Task 0 vs Task 3/5 (later consumers of shared projects) | .shproj/.projitems shape | Task 0 creates empty .projitems; later tasks add files — no conflict |

User decision: Microsoft.NETFramework.ReferenceAssemblies approved (implicit, build-only). net462 tests only run on Windows; on Linux they build but are not executed.

Ruling: Directory.Packages.props pins every SPEC §11 package except the VSIX packages — VSIX model is decided by Spike D (SP3) — cost if wrong: one line to add later.
Ruling: packages.lock.json / locked restore and nuget.config source mapping NOT added in M0 — plan Task 0 does not list them; SPEC §11 places locked restore in CI and source mapping in consumer repos (§4.3) — cost if wrong: add lock files in a later milestone.
Ruling: Work in harness worktree branch worktree-m0-scaffold; result pushed to claude/laughing-bohr-37djeu — designated push branch — cost if wrong: none (same commits).
Ruling: Placeholder body `Assert.IsTrue(true)` (plan Task 0 Step 2) is rejected by MSTEST0032 under TreatWarningsAsErrors (MSTest 4.5.1) — use `Assert.IsNotNull(typeof(ScaffoldTests).Assembly)`, keep test name Scaffold_Builds, no suppression, no older MSTest pin — CLAUDE.md warnings-as-errors outranks literal plan text — cost if wrong: one-line edit per test project.
Ruling: .shproj files kept on disk but not in dvx.sln — dotnet sln add rejects them on Linux (Microsoft.CodeSharing targets are VS-only); .projitems imports still compile shared content — cost if wrong: add from VS on Windows.
Task 0: minor (deferred): test namespaces are Dvx_Core_Tests style instead of Dvx.Core.Tests
Task 0: minor (deferred): Dvx.Analyzers lacks IsRoslynComponent/EnforceExtendedAnalyzerRules (belongs to Task 10)
Task 0: minor (deferred): .projitems import untested until shared content lands (Task 3)
Task 0: complete (commits 8566a4d..2e0f722, review clean)
Ruling: No separate final whole-branch review — session scope is one task and the task review covered the identical branch range 8566a4d..2e0f722 — cost if wrong: a scaffold issue surfaces in M1.

## Session 2 — milestone S-A + S-F (Tasks 1, 2)

Pre-flight scan:
| Item | Check | Finding |
|---|---|---|
| Task 1 self-consistency | files vs steps | consistent (throwaway spike + report) |
| Task 2 self-consistency | Step 2 needs VS 2026 + real CI image | not available in Linux container — partial only |
| Task 1 vs Task 3 | spike attribute copy vs Task 3 interface (Stage enum, string[] named args, DvxSdkInfo.Version const) | spike must use the same shapes as Task 3 so the result transfers |
| Task 2 vs Task 18 | Dvx.targets shape consumed later by vendoring | spike output informs Task 18; no conflict |
| Task 1/2 vs SPEC §11 | packages used | CrmSdk.CoreAssemblies, MetadataLoadContext, CodeAnalysis.CSharp 4.8.0, FakeItEasy, MSTest — all allowlisted; net462 ref assemblies = approved implicit package |

Ruling: Spike F verifies only `dotnet build` with .NET 8 SDK on Linux; VS 2026 and real CI image rows recorded as NOT CONFIRMED, decision = provisional go pending user verification on Windows — cost if wrong: M1 gate (S-F) not fully met until user verifies.
Ruling: Spike code committed under spikes/ (not in dvx.sln) so the user can re-run it on Windows/VS 2026 — plan calls it throwaway, not uncommitted — cost if wrong: delete folder.
Ruling: spikes/vendoring consumer is isolated from the dvx root Directory.Build.props/Directory.Packages.props (own files) to simulate a real consumer repo — cost if wrong: none.
Task 1: minor (deferred): doc should note Facades/ not in resolver set; enum decode via underlying type; null element in array untested; GetTypes ReflectionTypeLoadException inferred only
Task 1: complete (commits 0e4043f..7d76373, review clean)
Task 0: defect found in Task 1 — Directory.Build.props condition on TargetFramework=='net462' is evaluated before the csproj sets TargetFramework, so LangVersion 7.3 / Nullable disable never applied (M0 review missed it).
Ruling: fix in this session as a separate commit `fix: apply net462 LangVersion and Nullable in Directory.Build.targets` — move the net462 block to Directory.Build.targets so every net462 project gets it — it is a correctness bug in the delivered M0, not M1 work — cost if wrong: one file moved back.
Task 0 fix: complete (commit 8203aa0, controller reviewed 7-line diff; msbuild properties verified)
Task 2: review 1 — Needs fixes (Important: PackageVersion-in-vendored-targets analysis wrong/incomplete NU1506/NU1604; minors: DvxSpikeNoIvt spike-only note, abridged output, csc 4.11 vs VS host). Fix round 1 dispatched (FIX_BASE 8057978).
Task 2: fix round 1/5 (4 addressed, 0 open; commits 8057978..1bcfe62)
Task 2: minor (deferred): FakeItEasy pin detection ignores GlobalPackageReference/VersionOverride/floating pins; non-CPM consumer with own FakeItEasy reference gets duplicate — Task 18 to decide
Task 2: minor (deferred): analyzer project needs NoWarn RS2008 under TWAE (or release-tracking files) — Task 10
Task 2: complete (commits 8203aa0..1bcfe62, review clean after 1 fix round)

## Session 3 — milestone M1 (Tasks 3, 4, 5)

Environment: mono 6.8 installed via apt; net462 tests run with `dotnet test tests/Dvx.Plugin.Sdk.Tests -p:IsTestProject=true` (VSTest hosts net462 on mono; pass and fail both verified). Windows remains final confirmation (user).
User decision: run M1 tests on mono in container; csproj unchanged (root `dotnet test` still skips net462 on Linux).

Pre-flight scan:
| Pair / task | Produces vs consumes | Finding |
|---|---|---|
| Task 3 self | enum values vs Dataverse; markers vs attribute property names | values match Dataverse; `FilteringAttributes` is both a marker class and a property name — legal in C# (Color Color), compile check needed |
| Task 3 self | plan tests assert constants (Markers_AreStar, Stage_Values) | MSTEST0032/0025 may flag constant assertions under TWAE — see ruling |
| Task 3 → Task 4 | Stage/attributes → CustomApiBase parameter map via CustomApiRequestParameter/ResponseProperty | consistent |
| Task 4 self | CustomApi_MapsInputsAndOutputs uses Money input → request property Money (SPEC §6.2) | consistent |
| Task 4 self | statelessness rule vs static ConcurrentDictionary cache | allowed explicitly |
| Task 4 → Task 5 | PluginBase/EntityPluginBase/FlowBase consumed by Context_ExecutesEntityPlugin | consistent |
| Task 5 self | PluginTestContext.For(..., Stage) → Dvx.Testing depends on Dvx.Plugin.Sdk types | fine: both shared projects compile into same consumer test assembly |
| Task 4/5 vs SPEC §11 | FakeItEasy in Sdk.Tests (includes Dvx.Testing) + CrmSdk.CoreAssemblies | allowlisted |

Ruling: if MSTest analyzers flag plan-mandated constant assertions (Task 3), keep the test names and make them read the value at runtime (reflection on the const field / Convert.ToInt32 of a boxed enum) — no NoWarn — same reasoning as Task 0 ruling — cost if wrong: test bodies rewritten.
Ruling: Dvx.Testing's PluginTestContext may use FakeItEasy for IServiceProvider / IPluginExecutionContext / factory — SPEC §6.5 "FakeItEasy helpers", D29 — cost if wrong: replace with hand-written fakes.
Task 3: minor (deferred): AttributeUsage test does not pin CustomApi.Inherited / property-attribute AllowMultiple; DvxSdkInfo.Version assert lives inside usage test; test request props use decimal/object instead of Money/EntityReference
Task 3: complete (commits 2c53e74..e6c7747, review clean)
Task 4: minor (deferred): concurrency test would not reliably catch a runtime stored on the instance (no forced overlap, e.g. Barrier)
Task 4: minor (deferred): exception trace includes ex.Message (possible record data from platform messages)
Task 4: minor (deferred): PluginRuntime null context/factory gives NullReferenceException outside try
Task 4: minor (deferred): untested branches (non-assignable input, null response, Depth==MaxDepth, null provider); null output values unconfirmed in Dataverse
Task 4: open Dataverse question: ParameterCollection key case-sensitivity (exact name used)
Task 4: complete (commits e6c7747..db215a4, review clean)
Task 5: minor (deferred): query equality — raw int/Guid/decimal vs stored OptionSetValue/EntityReference/Money silently no match (most common query shape)
Task 5: minor (deferred): NotEqual-excludes-null and case-sensitive string equality are unverified Dataverse behaviors stated as fact
Task 5: minor (deferred): CloneValue shares EntityCollection/EntityReferenceCollection/OptionSetValueCollection/byte[]/AliasedValue; drops KeyAttributes/RowVersion
Task 5: minor (deferred): isolation test only covers strings; duplicate create, missing record, Delete, AllColumns, rejection paths untested
Task 5: minor (deferred): FakeItEasy dummies: ParentContext non-null at depth 1, GetService for unknown types returns dummy not null
Task 5: minor (deferred): RetrieveMultiple order unspecified (doc note)
Task 5: complete (commits db215a4..889c071, review clean)
Final review M1 (2c53e74..889c071): With fixes — Important: #1 query value normalisation + primary-key condition, #2 null attributes kept on Retrieve, #3 ParentContext/GetService dummies, #4 no parallel tests for EntityPluginBase/CustomApiBase. Minor #5 identity attribute props have public setters.
Ruling: string equality in InMemoryOrganizationService → OrdinalIgnoreCase, comment marks it INFERRED from Dataverse default collation (confirm in Spike C); NotEqual-excludes-null comment likewise marked inferred — cost if wrong: one comparer change.
Ruling: fix minor #5 in this wave — ctor-bound identity props (PluginStep Message/PrimaryEntity/Stage/Mode; CustomApi UniqueName; parameter/property UniqueName/Type) become `{ get; private set; }`; overrides my Task 3 decision "public setters" which was only needed for named args — cheap now, breaking after vendoring — cost if wrong: re-open setters.
Ruling: fix wave also takes minor #6 (allow PageInfo page 1, return all, MoreRecords=false) and #9 (doc: Outputs/Trace accumulate across Execute calls) and the non-assignable-input CustomApi test; #7 (WithTarget(EntityReference)) and #8 (trace "X: failed") stay deferred — cost if wrong: small follow-ups.
Spike C additions (for SPEC §13.1 Spike C scope, user to approve): ParameterCollection key casing; null output values accepted; string-equality case sensitivity; NotEqual null semantics.
Final fix wave: commit d236d1a, re-review: 8/8 addressed + additions OK, no new breakage.
Final: parked — null Equal/NotEqual value loud only when some record has the attribute — Ruling: real but minor; nothing downstream builds on it; carry to next Dvx.Testing touch.
Final: parked — Normalize OverflowException on NaN/Infinity, no unsigned widening; In operator and Guid-vs-string conditions NotSupported — Ruling: loud failures, acceptable for test helper.
Task 4/5 deferred #7 WithTarget(EntityReference), #8 "X: failed" trace — Ruling: stay deferred.
M1: complete (commits 2c53e74..d236d1a, final review clean after one fix wave)

## Session 4 — milestone M2 (Tasks 6, 7, 8)

Controller check: all 8 plan golden UUIDs re-computed independently with Python uuid.uuid5 over b3f5d2a1-7c4e-4f8a-9d61-2e0c5a7b9f13 — 8/8 match.

Pre-flight scan:
| Pair / task | Produces vs consumes | Finding |
|---|---|---|
| Task 6 self | `[DataTestMethod]` in golden test | MSTest 4 marks DataTestMethod obsolete → warning-as-error likely — see ruling |
| Task 6 self | key formats vs SPEC §7 / D33 | match; global step (no primary entity) not defined by SPEC — see ruling |
| Task 7 self | `[ClassInitialize]` runs nested `dotnet build` | fragile (nested build during test, locks, slow) — see ruling |
| Task 7 self | `referencePaths` for net462 refs (mscorlib, Xrm.Sdk) | source differs Windows (targeting pack) vs Linux (NuGet ref package) — see ruling |
| Task 7 self | AssemblyInfo.PublicKeyToken vs unsigned sample | sample unsigned in M2 — see ruling |
| Task 6 → Task 8 | IdentityKeys/Uuid5 → DesiredStateBuilder golden IDs | consistent (same keys) |
| Task 7 → Task 8 | AssemblyInfo/PluginStepInfo → Component fields | consistent; Fields value formats not specified (bools, filtering All marker, image attr order) — see ruling |
| Task 8 self | `describe --project <csproj>` needs build + reference paths | same mechanism as Task 7 — see ruling |
| Task 8 self | CommandLog repo root | SPEC: walk up for dvx.repo.json — see ruling |
| Task 8 vs SPEC §7 | duplicate IDs in one DLL = internal error (exit 9) | plan silent — include |

Ruling: golden tests use `[TestMethod]` + `[DataRow]` instead of `[DataTestMethod]` if MSTest 4 flags it; golden keys/values verbatim — cost if wrong: attribute name only.
Ruling: steps with null/empty primary entity (global messages) are rejected (DvxReadException / builder error) in M2 instead of inventing a key — SPEC §7 does not define it and identity freezes at M2 — ask user — cost if wrong: global-message plugins unsupported until decided.
Ruling: Sample.Plugins is built by MSBuild before tests via ProjectReference (ReferenceOutputAssembly=false) instead of a nested `dotnet build` in ClassInitialize — cost if wrong: switch back.
Ruling: build + reference discovery uses `dotnet msbuild <csproj> -t:Build -getProperty:TargetPath -getItem:ReferencePath` (JSON) — same path for tests and `describe` — gives the exact reference set the compiler used on any OS — cost if wrong: replace discovery helper.
Ruling: Sample.Plugins unsigned in M2; PublicKeyToken = "" when absent, lowercase hex otherwise — cost if wrong: field format change before M5.
Ruling: Component field value formats: numbers invariant; bools "true"/"false"; filteringattributes and image attributes sorted ordinal, comma-joined; `*` (All marker) → "" for both; absent → null; CustomApi ParentId = handler plugin type Id; components ordered by Kind then Key ordinal — cost if wrong: planner normalization in M5.
Ruling: CommandLog repo root = nearest ancestor with dvx.repo.json, else current directory — cost if wrong: log location only.
Ruling: duplicate component IDs within one DLL → DesiredStateBuilder throws internal error, CLI exit 9 (SPEC §7) — cost if wrong: none.
Task 6: minor (deferred): whitespace-only message/assembly/type accepted (entity rejects); Image(stepKey) not validated; no tr-TR culture test; no assembly-name case test
Task 6: complete (commits a32cd32..0e12918, review clean)
Ruling (refines build rulings): reference discovery helper lives in Dvx.Core (Task 7, reused by describe in Task 8): `dotnet msbuild <csproj> [-t:Build] -getProperty:TargetPath -getItem:ReferencePath`; tests rely on ProjectReference (ReferenceOutputAssembly=false) for building Sample.Plugins and call the helper WITHOUT the Build target, so parallel test projects never build the sample concurrently — cost if wrong: test flakiness, switch to a lock.
Ruling: AssemblyInfo.Culture = "neutral" when the assembly culture is empty (Dataverse convention) — cost if wrong: one string.
Ruling: Sample.Plugins sets AssemblyName/RootNamespace `Contoso.Plugins` so Task 8 golden IDs (asm:Contoso.Plugins, type Contoso.Plugins.AccountUpdatePreOperation) come from the real sample DLL — cost if wrong: none.
Task 7: minor (deferred): missing dllPath reported as DVX180; BadImageFormat/FileLoad not mapped; Convert.ToInt32 InvalidCast not mapped to DVX182; undefined enum values not validated; step tie-break (add Mode); weak no-load test
Task 7: note: DVX180–183 (reader/build codes) are not in SPEC §12/§10 — report to user
Task 7: complete (commits 0e12918..72b47ea, review clean)
Ruling: ProjectOutputResolver timeout (kill process tree → DVX181) and project path validation (GetFullPath + File.Exists → DVX181) are done in Task 8, because `describe` is the first production caller (build=true) — cost if wrong: none (small change in Task 7 file).
Ruling: CLI error output with --json: stdout `{"diagnostics":[DiagnosticDto…]}` plus the exit code; DvxReadException (DVX180–183, build failure DVX181) → exit 1; missing/invalid --project → exit 2; duplicate IDs or unexpected exception → exit 9 — SPEC §12 mapping — cost if wrong: snapshot updates.
Ruling: Component fields beyond the plan list: assembly `name`,`version`,`culture`,`publickeytoken`; customapi `displayname`; no image `messagepropertyname` (open SP2 question, not invented) — cost if wrong: field additions before M5.
Task 8: review 1 — Needs fixes (Important: unexpected exceptions exit 1 not 9 via System.CommandLine default handler; DVX001 collides with planned analyzer code; snapshots fragile under CRLF checkout on Windows). FIX_BASE 8789718.
Ruling: CLI usage/parse error code = DVX090 (free; outside analyzer 0nn rules and verify/reader 1xx/2xx ranges); duplicate IDs / internal = DVX190 per plan table (replaces DVX900); DVX184 kept for step without primary entity / invalid identity — cost if wrong: renumber before codes are documented in SPEC.
Ruling: CommandLog repo-root search also walks up from the project directory (implementer deviation) — accepted: logs land in the consumer repo even when run from elsewhere — cost if wrong: log location only.
Task 8: fix round 1/5 (3 Important + minors addressed, 0 open; commits 8789718..fa7b5fc)
Task 8: minor (deferred): stray indentation Program.cs:192; duplicate-ID vs unexpected-error wording; Build_IsDeterministic does not shuffle input; describe -t:Build vs Core.Tests concurrent reads of sample (theoretical)
Task 8: note for Task 9: extend existing Dvx.Core.DiagnosticCodes (do not add Verify/DiagnosticCodes.cs); add DVX090, DVX181–184 to the plan's code table
Task 8: complete (commits 72b47ea..fa7b5fc, review clean after 1 fix round)
Final review M2 (a32cd32..fa7b5fc): With fixes. Critical none; identity freeze verified (no ID leaks, goldens, determinism).
Ruling: fix wave takes I1 (`-restore` with build=true + unrestored-copy test), I3 (reject abstract and open-generic plugin types in the reader, new code DVX185 — freeze-safe), I4 code part (also reject primaryEntity "none"; SPEC §15/To-ask entry added by controller), I5 (tr-TR culture test with capital I inputs, assembly-name case test, keep-ID tests for rank/filtering/description/configuration/image columns), and minors: load/cast exceptions → exit 1 (DVX180/DVX182), read DvxSdkInfo.Version before attributes, log full command path (e.g. "sdk sync"), clear MSBuild* env vars + `-nodeReuse:false` in the child, stray indentation, `dvx describe` in CLAUDE.md Commands — cost if wrong: small reverts.
Ruling: I2 (same-key duplicates are user errors, not internal) deferred to Task 9 and written into the plan's Task 9 text — needs the verifier; builder keeps throwing for now — cost if wrong: user copy-paste mistakes report exit 9 until M3.
Ruling: deferred to M5 planner: ColumnList lowercasing and empty-array vs `*` distinction; image messagepropertyname source; signed sample assembly (DVX205) — recorded in plan Task 15/16 notes — cost if wrong: planner diffs.
Ruling: MSBuild output summarized in DVX181 (not streamed) stays for now; SourceFile null stays (M3 gets file/line from analyzers); broad ArgumentException→DVX184 catch stays — cost if wrong: diagnostics quality only.
Final fix wave: commit ad2a5de; re-review 9/9 addressed, no new breakage; goldens + describe.json unchanged.
Final: parked — `-restore` makes describe write obj/ and use network on fresh clone — Ruling: intended (SPEC §9.1 builds the project); document in help text later.
Final: parked — "none" check not trimmed (" none " passes) — Ruling: cosmetic, no such entity exists in Dataverse.
Final: parked — tr-TR freeze test fails without ICU (InvariantGlobalization) — Ruling: CI images have ICU; revisit if a runner lacks it.
Controller: SPEC §15 + To ask (global-message key; M2 codes), plan Task 9 + Task 15 notes, CLAUDE.md Status updated.
M2: complete (commits a32cd32..ad2a5de + docs, final review clean after one fix wave)
