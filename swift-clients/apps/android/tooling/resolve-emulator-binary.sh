#!/usr/bin/env bash
set -euo pipefail

sdk_root="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}"
[[ -n "$sdk_root" ]] || {
  echo "Android SDK root is required to locate the emulator executable" >&2
  exit 1
}

emulator_binary="$sdk_root/emulator/emulator"
[[ -f "$emulator_binary" && -x "$emulator_binary" ]] || {
  echo "Android emulator executable is missing or not executable: $emulator_binary" >&2
  exit 1
}
printf '%s\n' "$emulator_binary"
