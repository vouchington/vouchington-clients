#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${MISE_DATA_DIR:-}" ]]; then
  echo 'MISE_DATA_DIR must point to the job-scoped mise data directory.' >&2
  exit 1
fi
if [[ -z "${RUNNER_TEMP:-}" || "$MISE_DATA_DIR" != "$RUNNER_TEMP"/* ]]; then
  echo 'MISE_DATA_DIR must be a child of RUNNER_TEMP for Android CI.' >&2
  exit 1
fi
if ! command -v mise >/dev/null 2>&1; then
  echo 'mise is required; install it and run mise install swift from the repository root.' >&2
  exit 1
fi

swift_bin="$(mise which swift)" || {
  echo 'The mise-managed Swift toolchain is missing; run mise install swift.' >&2
  exit 1
}
toolchain_root="$(mise where swift)" || {
  echo 'Unable to resolve the mise-managed Swift installation root.' >&2
  exit 1
}
toolchain_root="$(CDPATH='' cd -- "$toolchain_root" && pwd -P)"
resolved_swift_bin="$(CDPATH='' cd -- "$(dirname -- "$swift_bin")" && pwd -P)/$(basename -- "$swift_bin")"
case "$resolved_swift_bin" in
  "$toolchain_root"/bin/swift | "$toolchain_root"/usr/bin/swift) ;;
  *)
    echo "mise resolved Swift outside its installation root: $swift_bin" >&2
    exit 1
    ;;
esac
mise_data_root="$(CDPATH='' cd -- "$MISE_DATA_DIR" && pwd -P)"
expected_root="$mise_data_root/installs/swift/6.4.0"

if [[ "$toolchain_root" != "$expected_root" || \
  ! -f "$toolchain_root/Info.plist" || \
  ! -x "$toolchain_root/usr/bin/swift" ]]; then
  echo "mise did not resolve the expected Swift 6.4.0 toolchain under $expected_root." >&2
  exit 1
fi

swift_version="$("$swift_bin" --version | awk 'NR == 1 { print $4 }')"
if [[ "$swift_version" != 6.4 && "$swift_version" != 6.4.0 ]]; then
  echo "Swift 6.4.0 managed by mise is required; found ${swift_version:-no Swift version}." >&2
  exit 1
fi

printf '%s\n' "$toolchain_root"
