#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 11 ]; then
  echo "usage: catalyst-compile-state.sh pack|unpack ARTIFACTS_ROOT STATE_DIR CANDIDATE_ROOT REVISION RUN_ID ATTEMPT RID SDK WORKLOAD XCODE" >&2
  exit 2
fi

mode=$1
artifacts_root=$2
state_dir=$3
candidate_root=$4
revision=$5
run_id=$6
attempt=$7
rid=$8
sdk=$9
workload=${10}
xcode=${11}
archive="$state_dir/compile-state.tar.gz"
source_archive="$state_dir/source-state.tar.gz"
manifest="$state_dir/compile-state.manifest"

verify_build_archive() {
  python3 - "$archive" <<'PY'
from pathlib import Path
import sys
import tarfile

archive = Path(sys.argv[1])
if archive.is_symlink() or not archive.is_file() or archive.stat().st_size > 16 * 1024 * 1024:
    raise SystemExit('missing, linked, or oversized managed archive')
with tarfile.open(archive, 'r:gz') as tar:
    members = tar.getmembers()
    if not 0 < len(members) <= 200 or sum(member.size for member in members) > 64 * 1024 * 1024:
        raise SystemExit('managed archive exceeds member or unpacked-size bound')
    seen = {}
    for member in members:
        if not (member.isfile() or member.isdir()):
            raise SystemExit('managed archive contains special or linked member')
        raw = member.name
        name = raw[:-1] if member.isdir() and raw.endswith('/') else raw
        parts = name.split('/')
        if (not name or any(part in ('', '.', '..') for part in parts)
                or parts[0] not in ('obj', 'bin')
                or (len(parts) == 1 and not member.isdir())
                or (member.isfile() and raw.endswith('/'))):
            raise SystemExit('managed archive path is not canonical under obj/bin')
        key = name.casefold()
        if key in seen:
            raise SystemExit('duplicate managed archive path')
        if any('/'.join(parts[:index]).casefold() in seen and
               seen['/'.join(parts[:index]).casefold()] == 'file'
               for index in range(1, len(parts))):
            raise SystemExit('managed archive file is an ancestor of another member')
        if member.isfile() and any(other.startswith(key + '/') for other in seen):
            raise SystemExit('managed archive file is an ancestor of another member')
        seen[key] = 'file' if member.isfile() else 'directory'
PY
}

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
    verify_build_archive
    python3 "$candidate_root/dotnet-clients/tooling/catalyst-source-snapshot.py" pack \
      "$candidate_root" "$source_archive" "$revision"
    printf '%s\nsha256=%s\nsource_sha256=%s\n' "$expected" \
      "$(shasum -a 256 "$archive" | cut -d ' ' -f 1)" \
      "$(shasum -a 256 "$source_archive" | cut -d ' ' -f 1)" > "$manifest"
    ;;
  unpack)
    [[ -f "$archive" && -f "$source_archive" && -f "$manifest" ]] || exit 1
    [[ "$(wc -l < "$manifest" | tr -d ' ')" == 9 ]] || exit 1
    [[ "$(sed -n '1,7p' "$manifest")" == "$expected" ]] || exit 1
    [[ "$(sed -n '8p' "$manifest")" == "sha256=$(shasum -a 256 "$archive" | cut -d ' ' -f 1)" ]] || exit 1
    [[ "$(sed -n '9p' "$manifest")" == "source_sha256=$(shasum -a 256 "$source_archive" | cut -d ' ' -f 1)" ]] || exit 1
    verify_build_archive
    tar -tzf "$archive" | awk '
      $0 !~ /^(obj|bin)\// { exit 1 }
      $0 ~ /(^|\/)\.\.($|\/)/ { exit 1 }
    ' || exit 1
    tar -tvzf "$archive" | awk 'substr($0, 1, 1) != "-" && substr($0, 1, 1) != "d" { exit 1 }' || exit 1
    python3 "$candidate_root/dotnet-clients/tooling/catalyst-source-snapshot.py" overlay \
      "$candidate_root" "$source_archive" "$revision"
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
