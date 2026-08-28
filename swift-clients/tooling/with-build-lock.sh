#!/usr/bin/env bash
set -euo pipefail

wait_seconds="${VOUCHA_BUILD_LOCK_WAIT_SECONDS:-60}"
on_acquire_timeout="${VOUCHA_BUILD_LOCK_ON_ACQUIRE_TIMEOUT:-}"
command_timeout="${VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS:-0}"

case "$wait_seconds" in
  '' | *[!0-9]* | 0)
    echo 'with-build-lock: VOUCHA_BUILD_LOCK_WAIT_SECONDS must be a positive integer no greater than 300' >&2
    exit 2
    ;;
esac
[ "$wait_seconds" -le 300 ] || {
  echo 'with-build-lock: VOUCHA_BUILD_LOCK_WAIT_SECONDS must be a positive integer no greater than 300' >&2
  exit 2
}
case "$on_acquire_timeout" in
  '' | fail | run-unlocked) ;;
  *)
    echo 'with-build-lock: VOUCHA_BUILD_LOCK_ON_ACQUIRE_TIMEOUT must be fail or run-unlocked' >&2
    exit 2
    ;;
esac
case "$command_timeout" in
  '' | *[!0-9]*)
    echo 'with-build-lock: VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS must be a nonnegative integer' >&2
    exit 2
    ;;
esac

if [ "${GITHUB_ACTIONS:-}" = true ]; then
  on_acquire_timeout="${on_acquire_timeout:-run-unlocked}"
  command_timeout="${VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS:-300}"
else
  on_acquire_timeout="${on_acquire_timeout:-fail}"
fi

exec pnpm exec vouchington with-host-lock \
  --name expensive-build \
  --timeout-seconds "$wait_seconds" \
  --command-timeout-seconds "$command_timeout" \
  --on-acquire-timeout "$on_acquire_timeout" \
  -- \
  "$@"
