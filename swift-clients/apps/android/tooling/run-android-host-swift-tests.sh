#!/usr/bin/env bash
# Host SwiftPM source tests for the Android package.
# Xcode 26.6 and Swift 6.3.3 are the supported pair. Swift 6.4 stays on the
# Android SDK and Gradle path and must not plan this package's host tests.
set -euo pipefail

script_dir="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
# Provision swift@6.3.3 and reject any other selected Xcode or compiler
# before SwiftPM loads the package graph.
bash "$script_dir/verify-android-host-swift.sh"

# The verifier installs the pinned toolchain. Keep mise from provisioning a
# different compiler, and keep dependency resolution on the frozen lock with
# no implicit keychain or netrc credentials.
exec env MISE_EXEC_AUTO_INSTALL=false mise exec swift@6.3.3 -- swift test \
  --force-resolved-versions \
  --disable-dependency-cache \
  --disable-keychain \
  --disable-netrc \
  "$@"
