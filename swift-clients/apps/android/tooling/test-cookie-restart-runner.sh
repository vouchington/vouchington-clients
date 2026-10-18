#!/usr/bin/env bash
set -euo pipefail

script_dir="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
runner="$script_dir/run-cookie-restart-emulator.sh"
instrumentation_invoker="$script_dir/invoke-cookie-instrumentation.sh"
emulator_resolver="$script_dir/resolve-emulator-binary.sh"
emulator_probe="$script_dir/probe-emulator-version.sh"
test_root="$(mktemp -d)"
trap 'rm -rf -- "$test_root"' EXIT
apk_dir="$test_root/apks"
revision="$(printf 'a%.0s' {1..40})"
mkdir -p "$test_root/bin"
cat > "$test_root/bin/emulator" <<'SCRIPT'
#!/usr/bin/env bash
: > "$VOUCHA_TEST_EMULATOR_MARKER"
exit 1
SCRIPT
chmod +x "$test_root/bin/emulator"
export PATH="$test_root/bin:$PATH"
export VOUCHA_TEST_EMULATOR_MARKER="$test_root/emulator-started"

sdk_root="$test_root/installed-android-sdk"
emulator_binary="$sdk_root/emulator/emulator"
if ANDROID_SDK_ROOT="$sdk_root" PATH=/usr/bin:/bin /bin/bash "$emulator_resolver" > "$test_root/emulator-path.log" 2>&1; then
  echo 'Missing Android SDK emulator executable was accepted' >&2
  exit 1
fi
grep -Fq "Android emulator executable is missing or not executable: $emulator_binary" "$test_root/emulator-path.log"
mkdir -p "$sdk_root/emulator"
printf '#!/usr/bin/env bash\nexit 0\n' > "$emulator_binary"
if ANDROID_SDK_ROOT="$sdk_root" PATH=/usr/bin:/bin /bin/bash "$emulator_resolver" > "$test_root/emulator-path.log" 2>&1; then
  echo 'Non-executable Android SDK emulator was accepted' >&2
  exit 1
fi
grep -Fq "Android emulator executable is missing or not executable: $emulator_binary" "$test_root/emulator-path.log"
chmod +x "$emulator_binary"
resolved_emulator="$(ANDROID_SDK_ROOT="$sdk_root" PATH=/usr/bin:/bin /bin/bash "$emulator_resolver")"
[[ "$resolved_emulator" == "$emulator_binary" ]] || {
  echo 'Android SDK emulator executable resolved outside the selected SDK' >&2
  exit 1
}
printf '#!/usr/bin/env bash\nprintf "Android Emulator 36.3.10\\n"\n' > "$emulator_binary"
[[ "$(bash "$emulator_probe" "$emulator_binary" "$test_root/emulator-failure.log")" == 'Android Emulator 36.3.10' ]]
[[ ! -e "$test_root/emulator-failure.log" ]]
printf '#!/usr/bin/env bash\necho "synthetic loader failure" >&2\nexit 127\n' > "$emulator_binary"
if bash "$emulator_probe" "$emulator_binary" "$test_root/emulator-failure.log" > "$test_root/emulator-version.log" 2>&1; then
  echo 'Android emulator version probe accepted a failing executable' >&2
  exit 1
else
  [[ "$?" -eq 127 ]] || {
    echo 'Android emulator version probe did not preserve exit status 127' >&2
    exit 1
  }
fi
grep -Fq 'Android SDK emulator version probe exited 127: synthetic loader failure' "$test_root/emulator-version.log"
grep -Fq 'Android SDK emulator version probe exited 127: synthetic loader failure' "$test_root/emulator-failure.log"

make_synthetic_artifacts() {
  rm -rf -- "$apk_dir"
  mkdir -p -- "$apk_dir"
  printf 'synthetic app APK bytes\n' > "$apk_dir/app-debug.apk"
  printf 'synthetic test APK bytes\n' > "$apk_dir/app-debug-androidTest.apk"
  printf '%s\n' "$revision" > "$apk_dir/candidate-revision.txt"
  (cd "$apk_dir" && sha256sum app-debug.apk app-debug-androidTest.apk > sha256sums.txt)
}

make_native_apk() {
  rm -rf -- "$test_root/native-archive"
  rm -f -- "$test_root/native.apk"
  mkdir -p "$test_root/native-archive/lib/arm64-v8a"
  local library
  for library in libSkipAndroidBridge.so libSwiftJNI.so libVouchaAndroid.so; do
    : > "$test_root/native-archive/lib/arm64-v8a/$library"
  done
  (cd "$test_root/native-archive" && zip -q -r "$test_root/native.apk" lib)
}

expect_rejection() {
  local expected_error="$1"
  shift
  if bash "$runner" "$@" > "$test_root/result.log" 2>&1; then
    echo "Expected cookie restart runner to reject: $expected_error" >&2
    exit 1
  fi
  grep -Fq "$expected_error" "$test_root/result.log" || {
    cat "$test_root/result.log" >&2
    echo "Cookie restart runner rejected for the wrong reason: $expected_error" >&2
    exit 1
  }
  [[ ! -e "$VOUCHA_TEST_EMULATOR_MARKER" ]] || {
    echo "Invalid artifact reached emulator startup" >&2
    exit 1
  }
}

# These are host-side parser checks with synthetic files. They are not an emulator result.
make_synthetic_artifacts
printf '%s\n' "$(printf 'b%.0s' {1..40})" > "$apk_dir/candidate-revision.txt"
expect_rejection 'Candidate revision mismatch' run "$apk_dir" "$revision"

make_synthetic_artifacts
printf 'changed\n' >> "$apk_dir/app-debug.apk"
expect_rejection 'APK checksum mismatch' run "$apk_dir" "$revision"

make_synthetic_artifacts
rm "$apk_dir/app-debug-androidTest.apk"
ln -s app-debug.apk "$apk_dir/app-debug-androidTest.apk"
expect_rejection 'Invalid instrumentation APK' run "$apk_dir" "$revision"

make_synthetic_artifacts
printf '%s  %s\n' "$(sha256sum "$apk_dir/app-debug.apk" | cut -d ' ' -f 1)" other.apk > "$apk_dir/sha256sums.txt"
expect_rejection 'APK checksum file list mismatch' run "$apk_dir" "$revision"

make_native_apk
bash "$runner" verify-native-abi "$test_root/native.apk"
mkdir -p "$test_root/native-archive/lib/x86_64"
: > "$test_root/native-archive/lib/x86_64/libandroidx.graphics.path.so"
(cd "$test_root/native-archive" && zip -q "$test_root/native.apk" lib/x86_64/libandroidx.graphics.path.so)
expect_rejection 'Candidate app APK advertises unsupported native ABI x86_64' verify-native-abi "$test_root/native.apk"

make_native_apk
rm "$test_root/native-archive/lib/arm64-v8a/libSkipAndroidBridge.so"
rm "$test_root/native.apk"
(cd "$test_root/native-archive" && zip -q -r "$test_root/native.apk" lib)
expect_rejection 'Candidate app APK lacks ARM64 libSkipAndroidBridge.so' verify-native-abi "$test_root/native.apk"

printf 'INSTRUMENTATION_CODE: -1\n' > "$test_root/instrumentation.log"
expect_rejection 'Instrumentation did not report one successful test' verify-instrumentation-output "$test_root/instrumentation.log"
printf '\nTime: 0.272\n\nOK (1 test)\n' > "$test_root/instrumentation.log"
expect_rejection 'Instrumentation did not report one successful test' verify-instrumentation-output "$test_root/instrumentation.log"
printf 'OK (1 test)\nINSTRUMENTATION_CODE: 0\n' > "$test_root/instrumentation.log"
expect_rejection 'Instrumentation did not report one successful test' verify-instrumentation-output "$test_root/instrumentation.log"
printf 'OK (1 test)\nINSTRUMENTATION_CODE: -1\n' > "$test_root/instrumentation.log"
bash "$runner" verify-instrumentation-output "$test_root/instrumentation.log"
cat > "$test_root/bin/adb" <<'SCRIPT'
#!/usr/bin/env bash
printf '%s\n' "$@" > "$VOUCHA_TEST_ADB_ARGUMENTS"
printf 'INSTRUMENTATION_RESULT: stream=\n\nTime: 0.272\n\nOK (1 test)\nINSTRUMENTATION_CODE: -1\n'
SCRIPT
chmod +x "$test_root/bin/adb"
export VOUCHA_TEST_ADB_ARGUMENTS="$test_root/adb-arguments.txt"
bash "$instrumentation_invoker" voucha.android.test CookieSnapshotBridgeRestartTest#seedCheckedEntries \
  > "$test_root/instrumentation.log"
cat > "$test_root/expected-adb-arguments.txt" <<'ARGS'
shell
am
instrument
-w
-r
-e
class
voucha.android.CookieSnapshotBridgeRestartTest#seedCheckedEntries
voucha.android.test/androidx.test.runner.AndroidJUnitRunner
ARGS
cmp "$test_root/expected-adb-arguments.txt" "$VOUCHA_TEST_ADB_ARGUMENTS"
bash "$runner" verify-instrumentation-output "$test_root/instrumentation.log"

# This host-only check exercises the runner's real shutdown helper. It does not
# stand in for a device or emulator test.
cat > "$test_root/cleanup-case.sh" <<'SCRIPT'
#!/usr/bin/env bash
set -euo pipefail
source "$1"
desired_status="$2"
RUNNER_TEMP="$3"
emulator_log="$3/emulator.log"
printf 'synthetic emulator diagnostic\n' > "$emulator_log"
python3 - "$3/emulator.pid" "$4" <<'PY' &
import os
import signal
import sys
import time

signal.signal(signal.SIGTERM, signal.SIG_IGN)
with open(sys.argv[1], "w", encoding="utf-8") as marker:
    marker.write(str(os.getpid()))
if sys.argv[2] == "stop":
    os.kill(os.getpid(), signal.SIGSTOP)
time.sleep(60)
PY
emulator_pid=$!
for _ in {1..100}; do
  [[ -f "$3/emulator.pid" ]] && break
  sleep 0.02
done
[[ -f "$3/emulator.pid" ]]
if [[ "$4" == stop ]]; then
  stopped=0
  for _ in {1..100}; do
    process_state="$(ps -o stat= -p "$emulator_pid" 2>/dev/null || true)"
    if [[ "$process_state" =~ ^[[:space:]]*T ]]; then
      stopped=1
      break
    fi
    sleep 0.02
  done
  [[ "$stopped" -eq 1 ]] || {
    echo 'Synthetic emulator never entered the stopped job state' >&2
    kill -KILL "$emulator_pid" 2>/dev/null || true
    wait "$emulator_pid" 2>/dev/null || true
    exit 1
  }
fi
if [[ "$4" == fail ]]; then
  stop_owned_emulator() {
    kill -KILL "$1" 2>/dev/null || true
    wait "$1" 2>/dev/null || true
    echo 'synthetic cleanup failure' >&2
    return 1
  }
fi
trap cookie_emulator_cleanup EXIT
exit "$desired_status"
SCRIPT
cat > "$test_root/bin/adb" <<'SCRIPT'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$VOUCHA_TEST_ADB_CALLS"
SCRIPT
cat > "$test_root/bin/timeout" <<'SCRIPT'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$VOUCHA_TEST_TIMEOUT_CALLS"
shift 2
"$@"
SCRIPT
chmod +x "$test_root/bin/adb" "$test_root/bin/timeout"
export VOUCHA_TEST_ADB_CALLS="$test_root/cleanup-adb-calls.log"
export VOUCHA_TEST_TIMEOUT_CALLS="$test_root/cleanup-timeout-calls.log"
python3 - "$script_dir/stop-owned-emulator.sh" "$test_root" <<'PY'
import os
import pathlib
import signal
import subprocess
import sys

helper, root = sys.argv[1:]
unrelated = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
try:
    for initial_status, expected_status, mode in ((0, 0, "term"), (7, 7, "term"),
                                                  (0, 0, "stop"), (0, 1, "fail"),
                                                  (7, 7, "fail")):
        case = pathlib.Path(root, f"cleanup-{initial_status}-{mode}")
        case.mkdir()
        command = ["bash", str(pathlib.Path(root, "cleanup-case.sh")), helper,
                   str(initial_status), str(case), mode]
        process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   text=True, start_new_session=True)
        try:
            stdout, stderr = process.communicate(timeout=18)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.communicate()
            raise AssertionError("cookie restart emulator cleanup exceeded its deadline")
        assert process.returncode == expected_status, (process.returncode, stdout, stderr)
        if mode == "fail":
            assert "synthetic cleanup failure" in stderr, stderr
        else:
            assert "ignored graceful shutdown; sending KILL" in stderr, stderr
        if initial_status == 7:
            assert "synthetic emulator diagnostic" in stderr, stderr
            assert (case / "voucha-cookie-emulator-failure-tail.log").read_text() == (
                "synthetic emulator diagnostic\n"
            )
        else:
            assert not (case / "voucha-cookie-emulator-failure-tail.log").exists()
        emulator_pid = int((case / "emulator.pid").read_text(encoding="utf-8"))
        try:
            os.kill(emulator_pid, 0)
        except ProcessLookupError:
            pass
        else:
            raise AssertionError("synthetic emulator survived cookie restart cleanup")
        assert unrelated.poll() is None, "cleanup killed an unrelated process"
finally:
    unrelated.terminate()
    unrelated.wait(timeout=5)
calls = pathlib.Path(root, "cleanup-adb-calls.log").read_text(encoding="utf-8")
assert calls.splitlines() == ["emu kill"] * 3, calls
timeouts = pathlib.Path(root, "cleanup-timeout-calls.log").read_text(encoding="utf-8")
assert timeouts.splitlines() == ["--kill-after=2s 5s adb emu kill"] * 3, timeouts
PY
echo 'Cookie restart host artifact, instrumentation, and cleanup checks passed'
