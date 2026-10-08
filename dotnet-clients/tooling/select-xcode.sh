#!/usr/bin/env bash
set -euo pipefail

required_version="${1:?Pass the Xcode version required by the pinned MAUI workload}"
applications_root="${2:-/Applications}"
: "${GITHUB_ENV:?GITHUB_ENV must name the workflow environment file}"

selected="$(xcode-select -p 2>/dev/null || true)"
shopt -s nullglob
candidates=("$selected" "$applications_root"/Xcode*.app/Contents/Developer)
for developer_dir in "${candidates[@]}"; do
  [[ -d "$developer_dir" ]] || continue
  developer_dir="$(cd -- "$developer_dir" && pwd -P)"
  xcodebuild_path="$developer_dir/usr/bin/xcodebuild"
  [[ -x "$xcodebuild_path" ]] || continue
  version="$(DEVELOPER_DIR="$developer_dir" "$xcodebuild_path" -version 2>/dev/null | awk 'NR == 1 { print $2 }')" || continue
  if [[ "$version" != "$required_version" && "$version" != "$required_version".* ]]; then
    continue
  fi
  # Mac Catalyst support is part of the macOS SDK, not a MacCatalyst.platform SDK.
  sdk="$(DEVELOPER_DIR="$developer_dir" xcrun --sdk macosx --show-sdk-path 2>/dev/null)" || continue
  [[ -f "$sdk/SDKSettings.plist" && -d "$sdk/System/iOSSupport" ]] || continue
  actool="$(DEVELOPER_DIR="$developer_dir" xcrun --sdk "$sdk" --find actool 2>/dev/null)" || continue
  [[ -n "$actool" && -x "$actool" ]] || continue
  printf 'DEVELOPER_DIR=%s\n' "$developer_dir" >>"$GITHUB_ENV"
  printf 'Selected Xcode %s at %s\n' "$version" "$developer_dir"
  exit 0
done

printf '::error::Xcode %s with a usable macOS SDK, Mac Catalyst support, and actool is required.\n' "$required_version" >&2
exit 1
