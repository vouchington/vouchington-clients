#!/usr/bin/env bash

# Sourced by the cookie restart runner so it can reap only its own emulator.
owned_emulator_stopped() {
  # jobs -p includes both running and SIGSTOPped children; jobs -pr does not.
  ! jobs -p | grep -Fxq "$1"
}

stop_owned_emulator() {
  local emulator_pid="$1"
  [[ "$emulator_pid" =~ ^[0-9]+$ ]] || {
    echo 'Cannot stop an emulator without its captured PID' >&2
    return 1
  }

  echo "Stopping owned Android emulator PID $emulator_pid" >&2
  timeout --kill-after=2s 5s adb emu kill >/dev/null 2>&1 || true

  if ! owned_emulator_stopped "$emulator_pid"; then
    kill -TERM "$emulator_pid" 2>/dev/null || true
    for _ in {1..20}; do
      owned_emulator_stopped "$emulator_pid" && break
      sleep 0.1
    done
  fi
  if ! owned_emulator_stopped "$emulator_pid"; then
    echo "Android emulator PID $emulator_pid ignored graceful shutdown; sending KILL" >&2
    kill -KILL "$emulator_pid" 2>/dev/null || true
    for _ in {1..20}; do
      owned_emulator_stopped "$emulator_pid" && break
      sleep 0.1
    done
  fi
  if ! owned_emulator_stopped "$emulator_pid"; then
    echo "Android emulator PID $emulator_pid could not be stopped within the cleanup deadline" >&2
    return 1
  fi
  wait "$emulator_pid" 2>/dev/null || true
  echo "Owned Android emulator PID $emulator_pid stopped and reaped" >&2
}

cookie_emulator_cleanup() {
  local exit_status=$?
  local log_path="${emulator_log:-}"
  if [[ "$exit_status" -ne 0 && -f "$log_path" ]]; then
    tail -n 120 "$log_path" > "${RUNNER_TEMP:-/tmp}/voucha-cookie-emulator-failure-tail.log" || true
    echo "Last 120 emulator log lines after cookie restart failure:" >&2 || true
    cat "${RUNNER_TEMP:-/tmp}/voucha-cookie-emulator-failure-tail.log" >&2 || true
  fi
  if [[ -n "$emulator_pid" ]]; then
    if ! stop_owned_emulator "$emulator_pid"; then
      [[ "$exit_status" -ne 0 ]] || exit_status=1
    fi
  fi
  exit "$exit_status"
}
