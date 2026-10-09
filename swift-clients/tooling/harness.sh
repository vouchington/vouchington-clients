#!/usr/bin/env bash
# swift-clients local harness — mirrors static-code-analysis/run-node-checks.mts
# Run from the repo root (not swift-clients/).
#
# Usage:
#   ./swift-clients/tooling/harness.sh              # run all checks
#   ./swift-clients/tooling/harness.sh --checks fmt,lint,lint-tests,ast-grep,build,periphery,generate,test
#   ./swift-clients/tooling/harness.sh --checks build-android
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
SWIFT_DIR="$ROOT_DIR/swift-clients"
SWIFT_COMMAND=()
XCODE_SWIFT_COMMAND=(xcrun swift)
IS_DARWIN=false
if [[ "$(uname)" == "Darwin" ]]; then
  IS_DARWIN=true
else
  # shellcheck source=dev/linux-native-images.sh
  # The path is resolved from the checkout at runtime.
  # shellcheck disable=SC1091
  source "$ROOT_DIR/dev/linux-native-images.sh"
fi

# Prefer Xcode.app toolchain for SourceKit-dependent tools (swiftlint and periphery).
# Without DEVELOPER_DIR pointing to Xcode.app, swiftlint crashes trying to load
# sourcekitdInProc.framework when the active developer directory is CommandLineTools.
if [[ -z "${DEVELOPER_DIR:-}" ]] && [[ -d "/Applications/Xcode.app/Contents/Developer" ]]; then
  export DEVELOPER_DIR="/Applications/Xcode.app/Contents/Developer"
fi

SWIFTLINT_CACHE_PATH="${SWIFTLINT_CACHE_PATH:-${TMPDIR:-/tmp}/voucha-swiftlint-cache}"
# ── parse --checks flag ───────────────────────────────────────────────────────
ALL_CHECKS=(fmt lint lint-tests ast-grep build periphery generate test)
AVAILABLE_CHECKS=("${ALL_CHECKS[@]}" build-android)
CHECKS=("${ALL_CHECKS[@]}")

is_known_check() {
  local known_check
  for known_check in "${AVAILABLE_CHECKS[@]}"; do
    [[ "$known_check" == "$1" ]] && return 0
  done
  return 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --checks)
      if [[ $# -lt 2 ]]; then
        echo "Error: --checks requires an argument" >&2
        exit 1
      fi
      IFS=',' read -ra CHECKS <<< "$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

for check in "${CHECKS[@]}"; do
  if ! is_known_check "$check"; then
    echo "Error: unknown check: $check" >&2
    exit 1
  fi
done

# ── helpers ───────────────────────────────────────────────────────────────────
PASS=0
FAIL=0
SKIPPED=0
CHECK_NAMES=()
CHECK_STATUSES=()
CHECK_ELAPSED_SECONDS=()
CHECK_CLASSIFICATIONS=()
CHECK_OUTPUT_FILES=()

cleanup_result_files() {
  local output_file
  for output_file in "${CHECK_OUTPUT_FILES[@]:-}"; do
    [[ -n "$output_file" ]] || continue
    rm -f -- "$output_file" || true
  done
}

trap cleanup_result_files EXIT

classify_failure() {
  local status="$1"
  local output_file="$2"

  if [[ "$status" -eq 124 ]] && grep -Eq '^with-host-lock: expensive-build command exceeded [0-9]*[1-9][0-9]*s; terminating its process group$' "$output_file"; then
    printf '%s\n' 'host-timeout'
  elif [[ "$status" -eq 1 ]] && grep -Eq '^with-host-lock: expensive-build lock not acquired within [0-9]*[1-9][0-9]*s$' "$output_file"; then
    printf '%s\n' 'lock-timeout'
  else
    printf '%s\n' 'check-failure'
  fi
}

record_result() {
  CHECK_NAMES+=("$1")
  CHECK_STATUSES+=("$2")
  CHECK_ELAPSED_SECONDS+=("$3")
  CHECK_CLASSIFICATIONS+=("$4")
}

terminate_output_line() {
  local output_file="$1"
  local last_byte

  [[ -s "$output_file" ]] || return 0
  last_byte="$(tail -c 1 "$output_file" | od -An -t x1)"
  [[ "$last_byte" == *0a* ]] || printf '\n'
}

run_check() {
  local name="$1"
  local output_file
  local start_seconds
  local elapsed_seconds
  local status
  local tee_status
  local classification
  local pipeline_status
  shift

  output_file="$(mktemp "${TMPDIR:-/tmp}/voucha-swift-harness.XXXXXX")" || {
    echo 'Error: cannot create Swift harness result file' >&2
    exit 2
  }
  CHECK_OUTPUT_FILES+=("$output_file")
  start_seconds=$SECONDS
  printf "  %-13s " "$name"
  if "$@" 2>&1 | tee "$output_file"; then
    pipeline_status=("${PIPESTATUS[@]}")
  else
    pipeline_status=("${PIPESTATUS[@]}")
  fi
  status="${pipeline_status[0]}"
  tee_status="${pipeline_status[1]}"
  elapsed_seconds=$((SECONDS - start_seconds))

  if [[ "$tee_status" -ne 0 ]]; then
    echo 'Error: cannot capture Swift harness check output' >&2
    exit 2
  fi
  terminate_output_line "$output_file"

  if [[ "$status" -eq 0 ]]; then
    echo "✓"
    PASS=$((PASS + 1))
  else
    echo "✗"
    FAIL=$((FAIL + 1))
  fi
  if [[ "$status" -eq 0 ]]; then
    classification='passed'
  else
    classification="$(classify_failure "$status" "$output_file")"
  fi
  record_result "$name" "$status" "$elapsed_seconds" "$classification"
}

run_in_directory() {
  local directory="$1"
  shift
  (cd -- "$directory" && "$@")
}

run_linux_swift_lint() {
  local image="$1"
  local workdir="$2"
  shift 2
  docker run --rm --read-only --network none \
    --user "$(id -u):$(id -g)" \
    --volume "$ROOT_DIR:/workspace:ro" \
    --tmpfs /tmp:rw,exec,mode=1777 \
    --workdir "$workdir" --env HOME=/tmp \
    "$image" "$@"
}

contains() {
  local needle="$1"
  for item in "${CHECKS[@]}"; do [[ "$item" == "$needle" ]] && return 0; done
  return 1
}

if contains build || contains test; then
  SWIFT_BIN="$(bash "$SWIFT_DIR/tooling/resolve-mise-swift.sh")" || exit 1
  SWIFT_COMMAND=(env MISE_EXEC_AUTO_INSTALL=false mise exec -- "$SWIFT_BIN")
fi

missing_periphery() {
  echo 'periphery is not installed; provision it with vouchington-github-actions-runners; see swift-clients/reference-readme-status.md#requirements' >&2
  return 127
}

print_failure_summary() {
  local index
  local output_file

  [[ "$FAIL" -gt 0 ]] || return 0

  echo 'failed checks:'
  echo 'check | classification | exit | elapsed'
  for index in "${!CHECK_NAMES[@]}"; do
    [[ "${CHECK_STATUSES[index]}" -ne 0 ]] || continue
    printf '%s | %s | %s | %ss\n' \
      "${CHECK_NAMES[index]}" \
      "${CHECK_CLASSIFICATIONS[index]}" \
      "${CHECK_STATUSES[index]}" \
      "${CHECK_ELAPSED_SECONDS[index]}"
    output_file="${CHECK_OUTPUT_FILES[index]}"
    echo 'diagnostic tail (last 40 lines):'
    if [[ -s "$output_file" ]]; then
      tail -n 40 "$output_file" || {
        echo 'Error: cannot read Swift harness diagnostic output' >&2
        exit 2
      }
      terminate_output_line "$output_file"
    else
      echo '(no diagnostic output)'
    fi
  done
}

echo "swift-clients harness"
echo "━━━━━━━━━━━━━━━━━━━━━"

# ── fmt: swiftformat --lint ───────────────────────────────────────────────────
if contains fmt; then
  if [[ "$IS_DARWIN" == true ]]; then
    run_check "fmt" swiftformat --lint "$SWIFT_DIR/"
  else
    run_check "fmt" run_linux_swift_lint "$SWIFTFORMAT_LINUX_IMAGE" /workspace \
      swift-clients/ --lint --quiet --verbose
  fi
fi

# ── lint: swiftlint --strict ──────────────────────────────────────────────────
if contains lint; then
  if [[ "$IS_DARWIN" == true ]]; then
    run_check "lint" run_in_directory "$SWIFT_DIR" swiftlint --strict --cache-path "$SWIFTLINT_CACHE_PATH"
  else
    run_check "lint" run_linux_swift_lint "$SWIFTLINT_LINUX_IMAGE" /workspace/swift-clients \
      --strict --cache-path /tmp/voucha-swiftlint
  fi
fi

# ── lint-tests: swiftlint test files with test-specific length cap ────────────
if contains lint-tests; then
  if [[ "$IS_DARWIN" == true ]]; then
    run_check "lint-tests" run_in_directory "$SWIFT_DIR" swiftlint --strict --config .swiftlint-tests.yml --cache-path "${SWIFTLINT_CACHE_PATH}-tests"
  else
    run_check "lint-tests" run_linux_swift_lint "$SWIFTLINT_LINUX_IMAGE" /workspace/swift-clients \
      --strict --config .swiftlint-tests.yml --cache-path /tmp/voucha-swiftlint-tests
  fi
fi

# ── ast-grep: Swift boundary rules ────────────────────────────────────────────
if contains ast-grep; then
  if [[ -z "${AST_GREP_BIN:-}" ]]; then
    if [[ -x "$ROOT_DIR/node_modules/@ast-grep/cli/ast-grep" ]]; then
      AST_GREP_BIN="$ROOT_DIR/node_modules/@ast-grep/cli/ast-grep"
    fi
  fi
  if [[ -n "${AST_GREP_BIN:-}" ]]; then
    run_check "ast-grep" run_in_directory "$ROOT_DIR" "$AST_GREP_BIN" scan --error --no-ignore hidden -- swift-clients/
  else
    echo "  ast-grep      - (skipped — repo-local ast-grep not found; run pnpm install at repo root)"
    SKIPPED=$((SKIPPED + 1))
  fi
fi

# ── build: swift build ────────────────────────────────────────────────────────
if contains build; then
  run_check "build/core" bash "$SWIFT_DIR/tooling/with-build-lock.sh" "${SWIFT_COMMAND[@]}" build --package-path "$SWIFT_DIR/core" --force-resolved-versions
  if [[ "$IS_DARWIN" == "true" ]]; then
    run_check "build/ui" bash "$SWIFT_DIR/tooling/with-build-lock.sh" "${XCODE_SWIFT_COMMAND[@]}" build --package-path "$SWIFT_DIR/ui" --force-resolved-versions
  else
    echo "  build/ui     - (skipped: macOS only; UI package is not Linux-portable yet)"
    SKIPPED=$((SKIPPED + 1))
  fi
fi

# ── build-android: Swift Android SDK bootstrap + core cross-compile ──────────
if contains build-android; then
  if [[ "$IS_DARWIN" == "true" ]]; then
    echo "  build-android - (skipped: Android cross-compile is Linux-oriented)"
    SKIPPED=$((SKIPPED + 1))
  else
    run_check "build-android" bash "$SWIFT_DIR/tooling/with-build-lock.sh" bash "$SWIFT_DIR/tooling/build-android-core.sh"
  fi
fi

# ── periphery: dead-code scan ─────────────────────────────────────────────────
if contains periphery; then
  if [[ "$IS_DARWIN" != "true" ]]; then
    echo "  periphery/core - (skipped: macOS only; packages are not Linux-portable yet)"
    echo "  periphery/ui   - (skipped: macOS only; packages are not Linux-portable yet)"
    SKIPPED=$((SKIPPED + 2))
  elif command -v periphery >/dev/null 2>&1; then
    run_check "periphery/core" bash "$SWIFT_DIR/tooling/with-build-lock.sh" bash "$SWIFT_DIR/tooling/periphery-scan.sh" core
    run_check "periphery/ui" env VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN=xcode bash "$SWIFT_DIR/tooling/with-build-lock.sh" bash "$SWIFT_DIR/tooling/periphery-scan.sh" ui
  else
    run_check "periphery" missing_periphery
  fi
fi

# ── generate: xcodegen project generation ────────────────────────────────────
if contains generate; then
  if [[ "$IS_DARWIN" == "true" ]]; then
    run_check "generate/macOS" run_in_directory "$ROOT_DIR" ./swift-clients/tooling/generate.sh macOS
    run_check "generate/iOS" run_in_directory "$ROOT_DIR" ./swift-clients/tooling/generate.sh iOS
  else
    echo "  generate/macOS - (skipped: macOS only; requires xcodegen)"
    echo "  generate/iOS   - (skipped: macOS only; requires xcodegen)"
    SKIPPED=$((SKIPPED + 2))
  fi
fi

# ── test: swift test ─────────────────────────────────────────────────────────
if contains test; then
  run_check "test/test-support" bash "$SWIFT_DIR/tooling/with-build-lock.sh" "${SWIFT_COMMAND[@]}" test --package-path "$SWIFT_DIR/test-support" --force-resolved-versions
  run_check "test/core" bash "$SWIFT_DIR/tooling/with-build-lock.sh" "${SWIFT_COMMAND[@]}" test --package-path "$SWIFT_DIR/core" --force-resolved-versions
  if [[ "$IS_DARWIN" == "true" ]]; then
    run_check "test/ui" bash "$SWIFT_DIR/tooling/with-build-lock.sh" "${XCODE_SWIFT_COMMAND[@]}" test --package-path "$SWIFT_DIR/ui" --force-resolved-versions
  else
    echo "  test/ui      - (skipped — macOS only)"
    SKIPPED=$((SKIPPED + 1))
  fi
fi

# ── summary ───────────────────────────────────────────────────────────────────
echo "━━━━━━━━━━━━━━━━━━━━━"
echo "  passed:  $PASS  failed:  $FAIL  skipped: $SKIPPED"
print_failure_summary

[[ $FAIL -eq 0 ]]
