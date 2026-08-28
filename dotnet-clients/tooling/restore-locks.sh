#!/usr/bin/env bash
set -euo pipefail

case "${1:-}" in
  verify) restore_mode=(--locked-mode) ;;
  update) restore_mode=(--force-evaluate) ;;
  *) echo 'Usage: dotnet-clients/tooling/restore-locks.sh <verify|update>' >&2; exit 64 ;;
esac

root="$(git rev-parse --show-toplevel)"
cd "$root/dotnet-clients"
while IFS='=' read -r inherited_name _; do
  normalized_name="$(printf '%s' "$inherited_name" | tr '[:upper:]' '[:lower:]')"
  case "$normalized_name" in
    artifactspath | baseintermediateoutputpath | baseoutputpath | dotnet_lock_artifacts_path | \
      msbuildprojectextensionspath | nuget_http_cache_path | nuget_packages | \
      nuget_plugins_cache_path | nuget_scratch | restorepackagespath | useartifactsoutput | \
      voucha_dotnet_cache_root | voucha_dotnet_temp_root)
      unset "$inherited_name"
      ;;
  esac
done < <(env)

artifacts_root="${VOUCHA_DOTNET_LOCK_ARTIFACTS_ROOT:-$root/.nuget/lock-artifacts}"
scratch_root="${VOUCHA_DOTNET_LOCK_SCRATCH_ROOT:-$root/.nuget/scratch}"
case "$artifacts_root" in /*) ;; *) echo 'VOUCHA_DOTNET_LOCK_ARTIFACTS_ROOT must be absolute' >&2; exit 2 ;; esac
case "$scratch_root" in /*) ;; *) echo 'VOUCHA_DOTNET_LOCK_SCRATCH_ROOT must be absolute' >&2; exit 2 ;; esac
export NUGET_PACKAGES="$root/.nuget/packages"
export NUGET_HTTP_CACHE_PATH="$root/.nuget/http-cache"
export NUGET_PLUGINS_CACHE_PATH="$root/.nuget/plugins-cache"
export NUGET_SCRATCH="$scratch_root"
export UseArtifactsOutput=true
mkdir -p "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH" "$NUGET_PLUGINS_CACHE_PATH" "$NUGET_SCRATCH" "$artifacts_root"

expected_sdk="$(jq -er '.sdk.version' "$root/global.json")"
expected_workload="$(jq -er '.sdk.workloadVersion' "$root/global.json")"
[[ "$(dotnet --version)" == "$expected_sdk" ]] || { echo "Expected .NET SDK $expected_sdk" >&2; exit 1; }
[[ "$(dotnet workload --version)" == "$expected_workload" ]] || { echo "Expected .NET workload set $expected_workload" >&2; exit 1; }

MSBuildEnableWorkloadResolver=false ArtifactsPath="$artifacts_root/portable" dotnet restore "${restore_mode[@]}" -p:Configuration=Release Voucha.DotNet.sln
ArtifactsPath="$artifacts_root/app-tests" dotnet restore "${restore_mode[@]}" -p:Configuration=Release tests/Voucha.Client.App.Tests/Voucha.Client.App.Tests.csproj
for project_kind in app core; do
  case "$project_kind" in
    app) project='src/Voucha.Client.App/Voucha.Client.App.csproj' ;;
    core) project='src/Voucha.Client.Core/Voucha.Client.Core.csproj' ;;
  esac
  for rid in maccatalyst-arm64 maccatalyst-x64; do
    ArtifactsPath="$artifacts_root/$project_kind-$rid" dotnet restore "${restore_mode[@]}" -p:Configuration=Release -p:TargetFramework=net10.0-maccatalyst "-p:RuntimeIdentifier=$rid" "$project"
  done
done

locks=(
  src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json
  src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json
  src/Voucha.Client.Core/packages.lock.json
  src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json
  src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json
  tests/Voucha.Client.App.Tests/packages.lock.json
  tests/Voucha.Client.Core.Tests/packages.lock.json
)
for lock in "${locks[@]}"; do
  [[ -f "$lock" && ! -L "$lock" ]] || { echo "Missing regular NuGet lock: dotnet-clients/$lock" >&2; exit 1; }
done
