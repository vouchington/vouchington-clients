#!/usr/bin/env bash
set -euo pipefail

package_dir="${1:?Android package directory is required}"
products_dir="$package_dir/.build/out/Products/Debug-android-aarch64"
runner="$products_dir/VouchaAndroidTests-test-runner"
legacy_bundle="$products_dir/voucha-androidPackageTests.xctest"

if [[ ! -x "$runner" ]]; then
  echo "Swift 6.4 Android test runner is missing or not executable: $runner" >&2
  exit 1
fi

if [[ -e "$legacy_bundle" || -L "$legacy_bundle" ]]; then
  if [[ ! -L "$legacy_bundle" || "$(readlink "$legacy_bundle")" != "$(basename "$runner")" ]]; then
    echo "Skip Android test bundle does not point to the verified runner: $legacy_bundle" >&2
    exit 1
  fi
else
  ln -s -- "$(basename "$runner")" "$legacy_bundle"
fi

if [[ ! -x "$legacy_bundle" ]]; then
  echo "Skip Android test bundle is missing or not executable: $legacy_bundle" >&2
  exit 1
fi
