#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -lt 1 ]]; then
  echo "Usage: $0 run|verify-artifact <apk-directory> <revision> | verify-instrumentation-output <file>" >&2
  exit 64
fi

fail() {
  echo "$1" >&2
  exit 1
}

verify_artifact() {
  local apk_dir="$1"
  local expected_revision="$2"
  [[ "$expected_revision" =~ ^[0-9a-f]{40}$ ]] || fail "Invalid candidate revision"
  [[ -f "$apk_dir/app-debug.apk" && ! -L "$apk_dir/app-debug.apk" ]] || fail "Invalid app APK"
  [[ -f "$apk_dir/app-debug-androidTest.apk" && ! -L "$apk_dir/app-debug-androidTest.apk" ]] || fail "Invalid instrumentation APK"
  [[ -f "$apk_dir/candidate-revision.txt" && ! -L "$apk_dir/candidate-revision.txt" ]] || fail "Invalid candidate revision file"
  [[ "$(cat "$apk_dir/candidate-revision.txt")" == "$expected_revision" ]] || fail "Candidate revision mismatch"
  [[ -f "$apk_dir/sha256sums.txt" && ! -L "$apk_dir/sha256sums.txt" ]] || fail "Invalid APK checksum file"
  [[ "$(awk '{ print $2 }' "$apk_dir/sha256sums.txt")" == $'app-debug.apk\napp-debug-androidTest.apk' ]] || fail "APK checksum file list mismatch"
  (cd "$apk_dir" && sha256sum --check --strict --status sha256sums.txt) || fail "APK checksum mismatch"
}

verify_instrumentation_output() {
  local output="$1"
  grep -Eq '^OK \(1 test\)$' <<<"$output" &&
    grep -Eq '^INSTRUMENTATION_CODE: -1$' <<<"$output"
}

mode="$1"
shift
if [[ "$mode" == verify-instrumentation-output ]]; then
  [[ "$#" -eq 1 && -f "$1" ]] || fail "Expected one instrumentation output file"
  verify_instrumentation_output "$(tr -d '\r' < "$1")" || fail "Instrumentation did not report one successful test"
  exit 0
fi
[[ "$mode" == run || "$mode" == verify-artifact ]] || fail "Unknown cookie restart mode"
[[ "$#" -eq 2 ]] || fail "Expected APK directory and candidate revision"
apk_dir="$1"
expected_revision="$2"
verify_artifact "$apk_dir" "$expected_revision"
if [[ "$mode" == verify-artifact ]]; then
  exit 0
fi

aapt="${ANDROID_HOME:?ANDROID_HOME is required}/build-tools/36.0.0/aapt"
[[ -x "$aapt" ]]
app_id="$("$aapt" dump badging "$apk_dir/app-debug.apk" | sed -n "s/^package: name='\([^']*\)'.*/\1/p")"
test_id="$("$aapt" dump badging "$apk_dir/app-debug-androidTest.apk" | sed -n "s/^package: name='\([^']*\)'.*/\1/p")"
[[ "$app_id" == ai.voucha.android && "$test_id" == ai.voucha.android.test ]] || {
  echo "Unexpected Android app or test package identity" >&2
  exit 1
}
apk_entries="$(unzip -Z1 "$apk_dir/app-debug.apk")"
grep -Eq '^lib/arm64-v8a/[^/]+\.so$' <<<"$apk_entries" || {
  echo "Candidate app APK has no ARM64 native library" >&2
  exit 1
}

avd_name="voucha-cookie-restart-${GITHUB_RUN_ID:-local}"
emulator_pid=
emulator_log="${RUNNER_TEMP:-/tmp}/voucha-cookie-emulator.log"
cleanup() {
  local status=$?
  if [[ "$status" -ne 0 && -f "$emulator_log" ]]; then
    tail -n 120 "$emulator_log" > "${RUNNER_TEMP:-/tmp}/voucha-cookie-emulator-failure-tail.log" || true
    echo "Last 120 emulator log lines after cookie restart failure:" >&2 || true
    cat "${RUNNER_TEMP:-/tmp}/voucha-cookie-emulator-failure-tail.log" >&2 || true
  fi
  if [[ -n "$emulator_pid" ]]; then
    timeout 15s adb emu kill >/dev/null 2>&1 || true
    kill "$emulator_pid" 2>/dev/null || true
    wait "$emulator_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT

[[ -c /dev/kvm && -r /dev/kvm && -w /dev/kvm ]] || {
  echo "Android emulator hardware acceleration is unavailable" >&2
  exit 1
}
script_dir="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
emulator_binary="$(bash "$script_dir/resolve-emulator-binary.sh")"
echo no | avdmanager create avd --force -n "$avd_name" -k 'system-images;android-35;google_apis;x86_64'
echo 'Android AVD creation completed; probing the selected SDK emulator executable'
emulator_version="$(bash "$script_dir/probe-emulator-version.sh" "$emulator_binary" \
  "${RUNNER_TEMP:-/tmp}/voucha-cookie-emulator-failure-tail.log")"
"$emulator_binary" -avd "$avd_name" -no-window -no-audio -no-boot-anim \
  -gpu swiftshader_indirect -no-snapshot -accel on -memory 2048 -cores 2 \
  >"$emulator_log" 2>&1 &
emulator_pid=$!

timeout 120s adb wait-for-device
booted=0
for _ in {1..90}; do
  if [[ "$(adb shell getprop sys.boot_completed | tr -d '\r')" == 1 ]]; then
    booted=1
    break
  fi
  sleep 2
done
[[ "$booted" == 1 ]] || {
  echo "Android emulator did not finish booting" >&2
  exit 1
}
sdk_level="$(adb shell getprop ro.build.version.sdk | tr -d '\r')"
build_fingerprint="$(adb shell getprop ro.build.fingerprint | tr -d '\r')"
abis="$(adb shell getprop ro.product.cpu.abilist | tr -d '\r')"
native_bridge="$(adb shell getprop ro.dalvik.vm.native.bridge | tr -d '\r')"
{
  printf 'Candidate revision: %s\n' "$expected_revision"
  printf 'Emulator version: %s\n' "$emulator_version"
  printf 'Android build fingerprint: %s\n' "$build_fingerprint"
  printf 'Android API: %s\n' "$sdk_level"
  printf 'Android ABIs: %s\n' "$abis"
  printf 'Android native bridge: %s\n' "$native_bridge"
} | tee "${RUNNER_TEMP:-/tmp}/voucha-cookie-runtime.txt"
[[ -n "$emulator_version" && -n "$build_fingerprint" ]] || {
  echo "Android emulator runtime identity is missing" >&2
  exit 1
}
[[ "$sdk_level" == 35 ]] || {
  echo "Android emulator did not boot the required API 35 image" >&2
  exit 1
}
[[ ",$abis," == *,arm64-v8a,* ]] || {
  echo "Android image cannot execute the candidate ARM64 app" >&2
  exit 1
}
[[ -n "$native_bridge" && "$native_bridge" != 0 ]] || {
  echo "Android image has no ARM native bridge" >&2
  exit 1
}

adb install -r "$apk_dir/app-debug.apk"
adb install -r "$apk_dir/app-debug-androidTest.apk"
adb shell pm path "$app_id" | grep -q '^package:'
adb shell pm list instrumentation | grep -Fq "$test_id/androidx.test.runner.AndroidJUnitRunner (target=$app_id)"

run_method() {
  local method="$1"
  local output
  if ! output="$(adb shell am instrument -w -e class "voucha.android.CookieSnapshotBridgeRestartTest#$method" \
    "$test_id/androidx.test.runner.AndroidJUnitRunner" | tr -d '\r')"; then
    printf '%s\n' "$output" >&2
    echo "Android instrumentation did not complete: $method" >&2
    return 1
  fi
  if ! verify_instrumentation_output "$output"; then
    printf '%s\n' "$output" >&2
    echo "Android cookie restart phase failed: $method" >&2
    return 1
  fi
  printf 'Android cookie restart phase passed: %s\n' "$method"
}

run_method seedCheckedEntries
adb shell am force-stop "$app_id"
run_method readEntriesAfterRestart
adb shell am force-stop "$app_id"
run_method removeOnlyCookieWithCheckedCommit
adb shell am force-stop "$app_id"
run_method readRemovalAfterRestart
