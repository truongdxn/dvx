# Spike F: vendoring and analyzer loading (throwaway)

Report: `docs/spikes/vendoring.md`. Not part of `dvx.sln`.

- `analyzer/` builds a trivial analyzer (`DVXSPIKE001`, class name ends with `Plugin`) as `Dvx.Analyzers.dll`
  against `Microsoft.CodeAnalysis.CSharp` 4.8.0 (netstandard2.0). It inherits the dvx root props.
- `consumer/` simulates a consumer repo with its own `Directory.Build.props` / `Directory.Packages.props`
  (central package management on, no TreatWarningsAsErrors). `build/dvx/Dvx.targets` is the vendored file;
  `build/dvx/analyzers/Dvx.Analyzers.dll` is the committed binary copy.

## Re-run (Linux or Windows with .NET 8 SDK)

```
./rebuild-analyzer.sh            # optional: rebuild and re-copy the analyzer (bash; on Windows copy bin/Release/netstandard2.0/Dvx.Analyzers.dll by hand)
cd consumer
dotnet build Contoso.sln         # expect 2x DVXSPIKE001 (plugin + test project), no CS8032 / AD0001
dotnet build Contoso.sln -p:DvxSpikeNoIvt=true   # expect CS0122 in the test project (negative check)
dotnet build src/Contoso.Plugins -getItem:Analyzer
```

## Extra steps for the user (NOT CONFIRMED in the Linux session)

Visual Studio 2026: open `consumer/Contoso.sln`, build. Check Error List (Build + IntelliSense) shows
`DVXSPIKE001` for `AccountPlugin` and `HelperPlugin`, Solution Explorer > Dependencies > Analyzers lists
`Dvx.Analyzers` with no warning icon, and Output shows no CS8032. Open `AccountPluginTests.cs`: no red
squiggle on `new AccountPlugin()`. Run the test in Test Explorer (net462, Windows only).

CI image: run `dotnet --info` and `dotnet build consumer/Contoso.sln -warnaserror-` on the real image and
confirm the same two warnings and no CS8032 / AD0001. Also run `dotnet test` there.
