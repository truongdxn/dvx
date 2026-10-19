# Spike A: attribute readability (throwaway)

Run (Linux/Windows, .NET 8 SDK): `dotnet build PluginLib` and `dotnet build Reader`, then
`dotnet Reader/bin/Debug/net8.0/Reader.dll PluginLib/bin/Debug/net462/SpikePlugins.dll <net462 reference dir>`
(flags: `--without-xrm-sdk`, `--only-dll-folder`, `--only-mscorlib`). On Windows the reference dir is
`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2`. Not part of `dvx.sln`.
Findings: `docs/spikes/attribute-read.md`.
