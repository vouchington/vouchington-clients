#!/usr/bin/env bash
# TEMPORARY: remove after the single JVM-only heap measurement.
set -euo pipefail

if [[ "$#" -lt 1 ]]; then
  echo 'Usage: measure-jvm-memory.sh <command> [arguments...]' >&2
  exit 64
fi

stop_owned_jobs() {
  local child
  for child in $(jobs -pr); do
    kill -TERM "$child" 2>/dev/null || true
  done
  wait 2>/dev/null || true
}

sample_memory() {
  trap stop_owned_jobs EXIT
  trap 'exit 0' TERM INT
  sysctl hw.memsize hw.logicalcpu hw.physicalcpu machdep.cpu.brand_string || true
  for ((sample = 1; sample <= 180; sample++)); do
    printf '\nsample=%s utc=%s\n' "$sample" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    ps -axo pid=,ppid=,rss=,ucomm= | awk '
      $NF ~ /(^|\/)java$/ {
        printf "%d %d %d java\n", $1, $2, $3
        if (++rows == 64) exit
      }
    ' || true
    vm_stat || true
    sysctl vm.swapusage || true
    sleep 5 &
    wait "$!" || return 0
  done
}

sample_memory > "${RUNNER_TEMP:?}/android-jvm-memory.log" 2>&1 &
cleanup() {
  local result=$?
  trap - EXIT
  stop_owned_jobs
  exit "$result"
}
trap cleanup EXIT
trap 'exit 143' TERM
trap 'exit 130' INT
"$@" &
wait "$!"
