#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 10 ]; then
  echo "usage: catalyst-compile-state.sh pack|unpack ARTIFACTS_ROOT STATE_DIR REVISION RUN_ID ATTEMPT RID SDK WORKLOAD XCODE" >&2
  exit 2
fi

mode=$1
artifacts_root=$2
state_dir=$3
revision=$4
run_id=$5
attempt=$6
rid=$7
sdk=$8
workload=$9
xcode=${10}
archive="$state_dir/compile-state.tar.gz"
manifest="$state_dir/compile-state.manifest"

[[ "$revision" =~ ^[0-9a-f]{40}$ ]] || exit 1
[[ "$run_id" =~ ^[0-9]+$ && "$attempt" =~ ^[0-9]+$ ]] || exit 1
[[ "$rid" == maccatalyst-arm64 || "$rid" == maccatalyst-x64 ]] || exit 1
[[ "$sdk" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || exit 1
[[ "$workload" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || exit 1
[[ "$xcode" =~ ^Xcode\ [0-9]+\.[0-9]+$ ]] || exit 1

expected=$(printf 'revision=%s\nrun_id=%s\nattempt=%s\nrid=%s\nsdk=%s\nworkload=%s\nxcode=%s\n' \
  "$revision" "$run_id" "$attempt" "$rid" "$sdk" "$workload" "$xcode")

case "$mode" in
  pack)
    [[ -d "$artifacts_root/obj/Voucha.Client.App" ]] || exit 1
    [[ -d "$artifacts_root/obj/Voucha.Client.Core" ]] || exit 1
    [[ -d "$artifacts_root/bin/Voucha.Client.Core" ]] || exit 1
    [[ -z "$(find "$artifacts_root/obj" "$artifacts_root/bin" -type l -print -quit)" ]] || exit 1
    mkdir -p "$state_dir"
    COPYFILE_DISABLE=1 tar -C "$artifacts_root" -czf "$archive" obj bin
    printf '%s\nsha256=%s\n' "$expected" "$(shasum -a 256 "$archive" | cut -d ' ' -f 1)" > "$manifest"
    ;;
  unpack)
    [[ -f "$archive" && -f "$manifest" ]] || exit 1
    [[ "$(wc -l < "$manifest" | tr -d ' ')" == 8 ]] || exit 1
    [[ "$(sed -n '1,7p' "$manifest")" == "$expected" ]] || exit 1
    [[ "$(sed -n '8p' "$manifest")" == "sha256=$(shasum -a 256 "$archive" | cut -d ' ' -f 1)" ]] || exit 1
    tar -tzf "$archive" | awk '
      $0 !~ /^(obj|bin)\// { exit 1 }
      $0 ~ /(^|\/)\.\.($|\/)/ { exit 1 }
    ' || exit 1
    tar -tvzf "$archive" | awk 'substr($0, 1, 1) != "-" && substr($0, 1, 1) != "d" { exit 1 }' || exit 1
    mkdir -p "$artifacts_root"
    tar -C "$artifacts_root" -xzf "$archive"
    [[ -f "$artifacts_root/obj/Voucha.Client.App/project.assets.json" ]] || exit 1
    [[ -f "$artifacts_root/obj/Voucha.Client.Core/project.assets.json" ]] || exit 1
    [[ -f "$artifacts_root/obj/Voucha.Client.App/release_net10.0-maccatalyst_${rid}/Voucha.Client.App.dll" ]] || exit 1
    ;;
  *)
    echo "unknown mode: $mode" >&2
    exit 2
    ;;
esac
