#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" != 0 ]]; then
  echo "Usage: $0" >&2
  exit 64
fi
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
# shellcheck source=swift-clients/tooling/cached-archive.sh
source "$SCRIPT_DIR/../../../tooling/cached-archive.sh"

# Match the full Android materializer's Skip pin. A regression checks all four
# values so host-only changes do not invalidate unrelated native archive keys.
SKIP_VERSION="1.9.13"
SKIP_MACOS_ZIP_URL="https://source.skip.tools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"
SKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"
SKIP_MACOS_ZIP_SHA256="8a0242c19a65e3cb0c1af0cbceb79a7294bd545b47748c007c0a69dc41c9bfdc"

if [[ -z "${VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR:-}" ]]; then
  echo "VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR must be set to the persistent archive cache." >&2
  exit 1
fi
DOWNLOAD_DIR="$VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR"
RUNNER_SWIFTPM_CACHE="$HOME/Library/Caches/org.swift.swiftpm"
mkdir -p -- "$DOWNLOAD_DIR"
SKIP_MACOS_ZIP_PATH="$DOWNLOAD_DIR/skip-macos-${SKIP_VERSION}.zip"
ensure_cached_archive \
  "$SKIP_MACOS_ZIP_URL" "$SKIP_MACOS_ZIP_PATH" sha256 "$SKIP_MACOS_ZIP_SHA256" \
  "Using cached Skip macOS binary zip at $SKIP_MACOS_ZIP_PATH"
for url in "$SKIP_MACOS_ZIP_URL" "$SKIP_MACOS_GITHUB_ZIP_URL"; do
  seed_swiftpm_binary_artifact \
    "$SKIP_MACOS_ZIP_PATH" "$RUNNER_SWIFTPM_CACHE" "$url" sha256 "$SKIP_MACOS_ZIP_SHA256"
  require_swiftpm_binary_artifact "$RUNNER_SWIFTPM_CACHE" "$url"
done
