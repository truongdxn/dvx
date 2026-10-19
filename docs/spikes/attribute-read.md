# Spike A: attribute readability

## Question

Can `Dvx.Core` (net8.0) read `[PluginStep]` registration attributes, `DvxSdkInfo.Version` and model
`Fields` constants from a built net462 plugin DLL using only `System.Reflection.MetadataLoadContext`
(never loading the assembly for execution)? Do `string[]` named arguments, enums, `null` and empty
arrays, repeated attributes and `internal` visibility survive? What must the resolver be given?

## Method

Code: `spikes/attribute-read/` (throwaway, not in `dvx.sln`).

- `Sdk/` is a shared project (`.shproj` + `.projitems`) holding the SDK shapes from the brief:
  `Stage`, `Mode`, `PluginStepAttribute` (ctor `(message, primaryEntity, stage, mode)`; named
  `FilteringAttributes`, `PreImageAttributes`, `PostImageAttributes` as `string[]`, `Rank`,
  `Description`), `DvxSdkInfo.Version`. All types `internal`. `PluginLib` imports it via
  `Sdk.projitems`, the source-inclusion pattern of the real product.
- `PluginLib/` (net462, C# 7.3, `Microsoft.CrmSdk.CoreAssemblies` 9.0.2.60) has `Account`/`Account.Fields`
  consts and three plugin classes implementing `Microsoft.Xrm.Sdk.IPlugin`: string[] args from const
  fields + empty array; two `[PluginStep]` on one class with explicit `null` and `Rank`; no named args.
- `Reader/` (net8.0, `System.Reflection.MetadataLoadContext` 8.0.1) uses `PathAssemblyResolver`
  with core assembly `mscorlib`, dumps `GetCustomAttributesData()` (constructor and named arguments, with
  raw `ArgumentType` / `Value` shapes), const fields via `FieldInfo.GetRawConstantValue()`, and probes
  `GetInterfaces()`. The program contains no `Assembly.Load*`/`LoadFrom`; plugin code is never executed.
- Resolver inputs: all `*.dll` in the plugin DLL's output folder (includes `Microsoft.Xrm.Sdk.dll`, copied
  local by the package) + all `*.dll` in the net462 reference folder (146 files total). Variants:
  without `Microsoft.Xrm.Sdk.dll`, DLL folder only, and `mscorlib.dll` only from the reference folder.

## Result

Everything required was readable. Trimmed output of the full run (146 resolver files):

```
Loaded: SpikePlugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
TYPE Dvx.Plugin.Sdk.DvxSdkInfo (IsPublic=False, IsNotPublic=True, ...)
  CONST Version = "0.1.0" (String)
TYPE Dvx.Plugin.Sdk.Stage ...
  CONST PreValidation = "10" (Int32)  CONST PreOperation = "20" (Int32)  CONST PostOperation = "40" (Int32)
TYPE SpikePlugins.Account+Fields (IsPublic=False, IsNotPublic=False, IsNestedAssembly=True)
  CONST Name = "name" (String)
  CONST CreditLimit = "creditlimit" (String)

TYPE SpikePlugins.AccountUpdatePlugin
  ATTR Dvx.Plugin.Sdk.PluginStepAttribute (IsNotPublic=True)
    ctor[0] message: ArgumentType=System.String Value.GetType()=String Value="Update"
    ctor[1] primaryEntity: ... Value="account"
    ctor[2] stage: ArgumentType=Dvx.Plugin.Sdk.Stage IsEnum=True Value.GetType()=Int32 Value=20 enumMember=PreOperation
    ctor[3] mode:  ArgumentType=Dvx.Plugin.Sdk.Mode  IsEnum=True Value.GetType()=Int32 Value=0  enumMember=Synchronous
    named FilteringAttributes: ArgumentType=System.String[] IsArray=True Value.GetType()=ReadOnlyCollection`1 Count=2 ["name", "creditlimit"]
    named PostImageAttributes: ArgumentType=System.String[] IsArray=True Value.GetType()=ReadOnlyCollection`1 Count=0 []
    named Description: ArgumentType=System.String Value="Validates credit limit"
  INTERFACES: Microsoft.Xrm.Sdk.IPlugin

TYPE SpikePlugins.AccountMultiPlugin          (two ATTR entries, in declaration order)
  ATTR ... ctor "Create","account", stage=40 (PostOperation), mode=1 (Asynchronous)
    named PreImageAttributes: ArgumentType=System.String[] IsArray=True Value=null
    named Rank: ArgumentType=System.Int32 Value=5
  ATTR ... ctor "Delete","account", stage=10 (PreValidation), mode=0 (Synchronous)
    named PreImageAttributes: ... ReadOnlyCollection`1 Count=1 ["name"]

TYPE SpikePlugins.AccountPlainPlugin
  ATTR ... ctor "Assign","account", 40, 0     (no named arguments listed at all)
```

Shapes:

| Source | `CustomAttributeTypedArgument` |
|---|---|
| `string` | `ArgumentType` = `System.String`, `Value` is `string` |
| enum ctor arg | `ArgumentType` = the enum type (`IsEnum` true, a metadata-only type), `Value` is the boxed underlying `int`, not an enum instance. Map to a name via `ArgumentType.GetFields()` + `GetRawConstantValue()`, or cast to the SDK-side enum in `Dvx.Core`. |
| `string[]` non-empty | `ArgumentType` = `System.String[]` (`IsArray` true), `Value` is `ReadOnlyCollection<CustomAttributeTypedArgument>`, each element `ArgumentType` = `System.String`, `Value` is `string` |
| `string[]` empty (`new string[0]`) | same type, `Count = 0` |
| `string[]` = `null` | `ArgumentType` = `System.String[]`, `Value = null` (named argument is present) |
| named arg not written in source | absent from `NamedArguments`; the property default (e.g. `Rank = 1`) is NOT in metadata, the reader must apply defaults |
| `int` named arg | `Value` is `int` |
| const fields | `GetRawConstantValue()` returns `string` / `int`; works on nested `internal` classes (`IsNestedAssembly`) |

Const expressions such as `Account.Fields.Name` and `Account.EntityLogicalName` are folded by the compiler,
so metadata holds the literal strings; no evaluation needed. Absent vs `null` vs empty are three distinct
states for arrays (not present / present with null `Value` / present with Count 0).

Resolver findings:

- Core assembly name `"mscorlib"` is required for net462 inputs. With only the DLL folder (no reference
  assemblies) the `MetadataLoadContext` constructor throws
  `FileNotFoundException: Could not find assembly 'mscorlib, Version=0.0.0.0, ...'`.
- `mscorlib.dll` from the v4.6.2 reference folder was sufficient for the whole run (`--only-mscorlib`,
  15 files: DLL folder + mscorlib): attributes, consts and `IPlugin` interface names all resolved. The
  full folder is not needed for this spike, but it is harmless and safer for plugins using other `System.*` types.
- `Microsoft.Xrm.Sdk.dll` missing: attribute decoding and const reading still succeed (the SDK attribute
  types live in the plugin DLL itself). Anything that resolves a type from it fails, e.g.
  `GetInterfaces()` throws
  `System.IO.FileNotFoundException: Could not find assembly 'Microsoft.Xrm.Sdk, Version=9.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35'. Either explicitly load this assembly using a method such as LoadFromAssemblyPath() or use a MetadataAssemblyResolver that returns a valid assembly.`
  Finding `IPlugin` implementors therefore needs it. In this run it was in the plugin output folder
  (`Microsoft.Xrm.Sdk.dll` was copied local by the `Microsoft.CrmSdk.CoreAssemblies` package).
- `internal` visibility is irrelevant: `PluginStepAttribute` and all enums are `IsNotPublic=True` and fully
  readable; `GetTypes()`, `GetCustomAttributesData()` and `GetRawConstantValue()` do not check accessibility.
- Not executed: no `Assembly.Load*`/`LoadFrom` anywhere in `Reader`; `MetadataLoadContext` loads metadata only.
  (Confirmed by code inspection of the spike; the program has no code path that runs plugin code.)

Reference path used: `~/.nuget/packages/microsoft.netframework.referenceassemblies.net462/1.0.3/build/.NETFramework/v4.6.2/`
(146 files with `Facades` excluded by `*.dll` top-level only, including `mscorlib.dll`).

Side finding (repo, not the spike): `Directory.Build.props` conditions on `'$(TargetFramework)' == 'net462'`,
but that file is evaluated before the SDK-style project body sets `TargetFramework`, so the net462 settings
(LangVersion 7.3, Nullable disabled) are not applied: the shared SDK source compiled with nullable enabled and
failed with CS8618. The spike csproj sets `LangVersion` and `Nullable` explicitly. Task 3 needs the same
fix (set them in each net462 csproj, or move the condition to a `Directory.Build.targets`).

## Decision

**Go. Keep the attribute shape as specified** (ctor-positional message/entity/stage/mode, `string[]` named
properties, `int Rank`, `string Description`, const-based names). No change needed.

## Confirmed vs inferred

Confirmed (run on Linux, .NET SDK 8.0.426, MetadataLoadContext 8.0.1):
- All argument kinds above decode as described; absent/null/empty arrays are distinguishable; repeated
  attributes appear as separate `CustomAttributeData` entries in declaration order (observed, but ordering
  is not a documented guarantee, so do not depend on it for identity: sort by the identity key).
- Shared-project source inclusion into a net462 library works and yields `internal` types.
- Missing `Microsoft.Xrm.Sdk.dll` and missing `mscorlib` failure modes and messages.

Inferred (not verified):
- On a Windows dev machine the reference folder is the targeting pack
  (`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2`); in CI without
  the pack it would be the `Microsoft.NETFramework.ReferenceAssemblies.net462` package (as used here).
  Alternative: resolve `mscorlib` from the .NET Framework runtime directory
  (`%windir%\Microsoft.NET\Framework64\v4.0.30319`); untested.
- Behaviour with a plugin that is ILMerged or targets another net4x version (version-unification of
  `mscorlib` references: `MetadataLoadContext` matched `mscorlib` 4.0.0.0 by simple name here).
- That the Windows run gives identical output (nothing platform-specific is used).

## Implications for Task 7 (AttributeReader)

- Resolver inputs: (1) all `*.dll` next to the plugin DLL (must contain `Microsoft.Xrm.Sdk.dll`, otherwise
  report a clear diagnostic: type resolution fails with `FileNotFoundException`); (2) .NET Framework reference
  assemblies, at minimum `mscorlib.dll` (core assembly name `"mscorlib"`), preferably the whole v4.6.2 folder;
  tests can use the NuGet reference-assembly package so they work without a Windows targeting pack.
- Match attributes by `AttributeType.FullName` (`Dvx.Plugin.Sdk.PluginStepAttribute`), not by type identity;
  there is no shared type between `Dvx.Core` and the plugin.
- Decode enums from `Value` as `int`, validate against `Stage`/`Mode` defined in Core (reject undefined values
  with a diagnostic).
- Decode arrays: `Value is IReadOnlyCollection<CustomAttributeTypedArgument>` (actually `ReadOnlyCollection`);
  take each element's `Value as string`. Distinguish `null` `Value` (explicit null), empty collection, and
  absent named argument; apply SDK defaults (`Rank = 1`) in the reader when absent.
- Read consts with `GetRawConstantValue()` only if needed; attribute args are already folded literals.
- Read `DvxSdkInfo.Version` via `GetType("Dvx.Plugin.Sdk.DvxSdkInfo").GetField("Version").GetRawConstantValue()`.
- Dispose the `MetadataLoadContext`; wrap `GetInterfaces()`/`GetTypes()` in handling for
  `FileNotFoundException` and `ReflectionTypeLoadException`.
