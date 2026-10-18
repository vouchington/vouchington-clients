#!/usr/bin/env bash
set -euo pipefail

script_dir="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
runner="$script_dir/run-cookie-restart-emulator.sh"
emulator_resolver="$script_dir/resolve-emulator-binary.sh"
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

make_synthetic_artifacts() {
  rm -rf -- "$apk_dir"
  mkdir -p -- "$apk_dir"
  printf 'synthetic app APK bytes\n' > "$apk_dir/app-debug.apk"
  printf 'synthetic test APK bytes\n' > "$apk_dir/app-debug-androidTest.apk"
  printf '%s\n' "$revision" > "$apk_dir/candidate-revision.txt"
  (cd "$apk_dir" && sha256sum app-debug.apk app-debug-androidTest.apk > sha256sums.txt)
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

printf 'INSTRUMENTATION_CODE: -1\n' > "$test_root/instrumentation.log"
expect_rejection 'Instrumentation did not report one successful test' verify-instrumentation-output "$test_root/instrumentation.log"
printf 'OK (1 test)\nINSTRUMENTATION_CODE: 0\n' > "$test_root/instrumentation.log"
expect_rejection 'Instrumentation did not report one successful test' verify-instrumentation-output "$test_root/instrumentation.log"
printf 'OK (1 test)\nINSTRUMENTATION_CODE: -1\n' > "$test_root/instrumentation.log"
bash "$runner" verify-instrumentation-output "$test_root/instrumentation.log"
echo 'Cookie restart host artifact and instrumentation parsers passed synthetic checks'
