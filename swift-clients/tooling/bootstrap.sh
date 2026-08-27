#!/usr/bin/env bash
set -euo pipefail

REQUIRED_XCODEGEN="2.42"
REQUIRED_SWIFTFORMAT="0.54"
REQUIRED_SWIFTLINT="0.57"
REQUIRED_PERIPHERY="3.0"

check_version() {
    local tool="$1"
    local required="$2"
    local actual

    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "✗ $tool not found — provision it with vouchington-github-actions-runners; see docs/development/system-dependencies.md"
        return 1
    fi

    case "$tool" in
        xcodegen)    actual=$(xcodegen version 2>&1 | grep -oE '[0-9]+\.[0-9]+' | head -1) ;;
        swiftformat) actual=$(swiftformat --version 2>&1 | grep -oE '[0-9]+\.[0-9]+' | head -1) ;;
        swiftlint)   actual=$(swiftlint version 2>&1 | grep -oE '[0-9]+\.[0-9]+' | head -1) ;;
        periphery)   actual=$(periphery version 2>&1 | grep -oE '[0-9]+\.[0-9]+' | head -1) ;;
    esac

    if [[ "$(printf '%s\n' "$required" "$actual" | sort -V | head -1)" == "$required" ]]; then
        echo "✓ $tool $actual (≥ $required required)"
    else
        echo "✗ $tool $actual is below required $required"
        return 1
    fi
}

echo "Checking swift-clients prerequisites..."
check_version xcodegen    "$REQUIRED_XCODEGEN"
check_version swiftformat "$REQUIRED_SWIFTFORMAT"
check_version swiftlint   "$REQUIRED_SWIFTLINT"
check_version periphery   "$REQUIRED_PERIPHERY"
echo "All prerequisites satisfied."
