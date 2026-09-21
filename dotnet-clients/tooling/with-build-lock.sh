#!/usr/bin/env bash
set -u

fail() {
  echo "with-build-lock: $1" >&2
  exit 2
}

wait_seconds="${VOUCHA_BUILD_LOCK_WAIT_SECONDS:-60}"
on_acquire_timeout="${VOUCHA_BUILD_LOCK_ON_ACQUIRE_TIMEOUT:-}"
command_timeout="${VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS:-0}"

case "$wait_seconds" in
  '' | *[!0-9]* | 0) fail 'VOUCHA_BUILD_LOCK_WAIT_SECONDS must be a positive integer no greater than 300' ;;
esac
[ "$wait_seconds" -le 300 ] || fail 'VOUCHA_BUILD_LOCK_WAIT_SECONDS must be a positive integer no greater than 300'
case "$on_acquire_timeout" in
  '' | fail | run-unlocked) ;;
  *) fail 'VOUCHA_BUILD_LOCK_ON_ACQUIRE_TIMEOUT must be fail or run-unlocked' ;;
esac
case "$command_timeout" in
  '' | *[!0-9]*) fail 'VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS must be a nonnegative integer' ;;
esac

if [ "${GITHUB_ACTIONS:-}" = true ]; then
  on_acquire_timeout="${on_acquire_timeout:-run-unlocked}"
  command_timeout="${VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS:-300}"
else
  on_acquire_timeout="${on_acquire_timeout:-fail}"
fi

root_dir="$(CDPATH='' cd -- "$(dirname -- "$0")/../.." && pwd)"
cd "$root_dir" || fail 'repository root is unavailable'
exec npx --yes pnpm@11.13.1 exec vouchington with-host-lock \
  --name expensive-build \
  --timeout-seconds "$wait_seconds" \
  --command-timeout-seconds "$command_timeout" \
  --on-acquire-timeout "$on_acquire_timeout" \
  -- \
  "$@"
