#!/usr/bin/env bash
# Builds the spike analyzer and copies it to the vendored location of the simulated consumer.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dotnet build "$here/analyzer/Spike.Analyzers.csproj" -c Release
cp "$here/analyzer/bin/Release/netstandard2.0/Dvx.Analyzers.dll" "$here/consumer/build/dvx/analyzers/Dvx.Analyzers.dll"
