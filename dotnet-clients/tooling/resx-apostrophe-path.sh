#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ROOT_DIR="$(cd "$DOTNET_DIR/.." && pwd)"
FIXTURE_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/voucha-resx-path.XXXXXX")"
TEST_ROOT="$FIXTURE_ROOT/path &'s <xml>"
if ! mkdir -p "$TEST_ROOT"; then
  TEST_ROOT="$FIXTURE_ROOT/apostrophe's"
  mkdir -p "$TEST_ROOT"
fi
trap 'rm -rf "$FIXTURE_ROOT"' EXIT

xml_escape() {
  node -e \
    'process.stdout.write(process.argv[1].replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll("\"", "&quot;").replaceAll("'"'"'", "&apos;"))' \
    "$1"
}

DOTNET_DIR_XML="$(xml_escape "$DOTNET_DIR")"
ARTIFACTS_PATH="$TEST_ROOT/artifact's"

export DOTNET_CLI_HOME="$TEST_ROOT/dotnet-home"
export NUGET_HTTP_CACHE_PATH="$TEST_ROOT/nuget-http-cache"
export NUGET_PACKAGES="$TEST_ROOT/nuget-packages"
export NUGET_PLUGINS_CACHE_PATH="$TEST_ROOT/nuget-plugins-cache"
export NUGET_SCRATCH="$TEST_ROOT/nuget-scratch"
export MSBuildEnableWorkloadResolver=false

cp -- "$ROOT_DIR/global.json" "$TEST_ROOT/global.json"

cat > "$TEST_ROOT/Messages.resx" <<'RESX'
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Greeting" xml:space="preserve"><value>Hello</value></data>
</root>
RESX

cat > "$TEST_ROOT/Messages.es.resx" <<'RESX'
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="Greeting" xml:space="preserve"><value>Hola</value></data>
</root>
RESX

cat > "$TEST_ROOT/Program.cs" <<'CSHARP'
using System.Globalization;
using System.Resources;

var resources = new ResourceManager("ResxPathFixture.Messages", typeof(Program).Assembly);
if (resources.GetString("Greeting", CultureInfo.GetCultureInfo("es")) != "Hola")
{
  throw new InvalidOperationException("The Spanish satellite resource was not loaded.");
}
CSHARP

cat > "$TEST_ROOT/ResxPathFixture.csproj" <<CSHARP
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$DOTNET_DIR_XML/Directory.Build.targets" />
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>ResxPathFixture</RootNamespace>
  </PropertyGroup>
</Project>
CSHARP

cd "$TEST_ROOT"
dotnet run \
  --project "$TEST_ROOT/ResxPathFixture.csproj" \
  --configuration Release \
  -p:UseArtifactsOutput=true \
  -p:ArtifactsPath="$ARTIFACTS_PATH" \
  --artifacts-path "$ARTIFACTS_PATH"
