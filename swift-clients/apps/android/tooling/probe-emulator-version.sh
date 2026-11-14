#!/usr/bin/env bash
set -euo pipefail

[[ "$#" -eq 2 ]] || {
  echo 'Expected Android emulator executable and failure log path' >&2
  exit 64
}

if version_output="$("$1" -version 2>&1)"; then
  printf '%s\n' "${version_output%%$'\n'*}"
else
  status=$?
  diagnostic="Android SDK emulator version probe exited $status: ${version_output:0:4096}"
  printf '%s\n' "$diagnostic" >&2
  printf '%s\n' "$diagnostic" > "$2" || true
  exit "$status"
fi
