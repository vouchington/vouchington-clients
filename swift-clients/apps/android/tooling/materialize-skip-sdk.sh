#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
# shellcheck source=swift-clients/tooling/cached-archive.sh
source "$SCRIPT_DIR/../../../tooling/cached-archive.sh"

SWIFT_VERSION="6.4.0"
SWIFT_TOOLCHAIN_ID="swift-${SWIFT_VERSION}-RELEASE"
SWIFT_ANDROID_SDK_ID="${SWIFT_TOOLCHAIN_ID}_android"
SWIFT_ANDROID_SDK_URL="https://download.swift.org/swift-${SWIFT_VERSION}-release/android-sdk/${SWIFT_TOOLCHAIN_ID}/${SWIFT_ANDROID_SDK_ID}.artifactbundle.tar.gz"
SWIFT_ANDROID_SDK_CHECKSUM="21fb555122a3d801ad943d48df7ebffdd8824de61c25c180bb792d3edaee0b43"
ANDROID_NDK_VERSION="r30"
ANDROID_NDK_URL="https://dl.google.com/android/repository/android-ndk-${ANDROID_NDK_VERSION}-darwin.zip"
ANDROID_NDK_SHA1="c060be96767eefbb8e0a27796d6f43115fc1a0c4"
SKIP_VERSION="1.9.13"
SKIP_MACOS_ZIP_URL="https://source.skip.tools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"
SKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"
SKIP_MACOS_ZIP_SHA256="8a0242c19a65e3cb0c1af0cbceb79a7294bd545b47748c007c0a69dc41c9bfdc"

if [[ -z "${VOUCHA_SKIP_SWIFT_HOME:-}" ]]; then
  echo "VOUCHA_SKIP_SWIFT_HOME must be set to a job-scoped home." >&2
  exit 1
fi
if [[ -z "${RUNNER_TEMP:-}" || "$VOUCHA_SKIP_SWIFT_HOME" != "$RUNNER_TEMP"/* ]]; then
  echo "VOUCHA_SKIP_SWIFT_HOME must be a child of RUNNER_TEMP." >&2
  exit 1
fi
if [[ -z "${VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR:-}" ]]; then
  echo "VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR must be set to the persistent archive cache." >&2
  exit 1
fi
RUNNER_SWIFTPM_CACHE="$HOME/Library/Caches/org.swift.swiftpm"

HOME="$VOUCHA_SKIP_SWIFT_HOME"
CFFIXED_USER_HOME="$VOUCHA_SKIP_SWIFT_HOME"
SWIFT_SDKS_DIR="$HOME/Library/org.swift.swiftpm/swift-sdks"
export HOME CFFIXED_USER_HOME SWIFT_SDKS_DIR

SWIFT_TMP="${RUNNER_TEMP}/swift-tmp"
mkdir -p -- "$SWIFT_TMP" "$VOUCHA_SKIP_SWIFT_HOME"
TMPDIR="$SWIFT_TMP"
export TMPDIR

DOWNLOAD_DIR="$VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR"
mkdir -p -- "$DOWNLOAD_DIR"

SDK_TAR_PATH="$DOWNLOAD_DIR/${SWIFT_ANDROID_SDK_ID}.artifactbundle.tar.gz"
NDK_ZIP_PATH="$DOWNLOAD_DIR/android-ndk-${ANDROID_NDK_VERSION}-darwin.zip"
SKIP_MACOS_ZIP_PATH="$DOWNLOAD_DIR/skip-macos-${SKIP_VERSION}.zip"

TOOLCHAIN_DIR="$HOME/toolchains/${SWIFT_TOOLCHAIN_ID}.xctoolchain"
SDK_BUNDLE="$HOME/Library/org.swift.swiftpm/swift-sdks/${SWIFT_ANDROID_SDK_ID}.artifactbundle"
SDK_ROOT="$SDK_BUNDLE/swift-android"
NDK_DIR="$SDK_ROOT/android-ndk-${ANDROID_NDK_VERSION}"
NDK_SENTINEL="$NDK_DIR/.extraction-complete"
NDK_SYSROOT_LIB="$SDK_ROOT/ndk-sysroot/usr/lib/aarch64-linux-android"
SDK_INFO="$SDK_BUNDLE/info.json"
MISE_SWIFT_TOOLCHAIN_ROOT="$(bash "$SCRIPT_DIR/resolve-mise-swift-toolchain.sh")"
MISE_SWIFT_BIN="$(mise which swift)"

mkdir -p -- "$HOME/toolchains"
if [[ -L "$TOOLCHAIN_DIR" ]]; then
  if [[ "$(CDPATH='' cd -- "$TOOLCHAIN_DIR" && pwd -P)" != "$MISE_SWIFT_TOOLCHAIN_ROOT" ]]; then
    echo "Skip toolchain alias points outside the mise installation: $TOOLCHAIN_DIR" >&2
    exit 1
  fi
elif [[ -e "$TOOLCHAIN_DIR" ]]; then
  echo "Skip toolchain alias must be a symlink to the mise installation: $TOOLCHAIN_DIR" >&2
  exit 1
else
  ln -s -- "$MISE_SWIFT_TOOLCHAIN_ROOT" "$TOOLCHAIN_DIR"
fi
TOOLCHAIN_SWIFT="$TOOLCHAIN_DIR/usr/bin/swift"

ensure_cached_archive \
  "$SWIFT_ANDROID_SDK_URL" \
  "$SDK_TAR_PATH" \
  sha256 \
  "$SWIFT_ANDROID_SDK_CHECKSUM" \
  "Using cached Swift Android SDK archive at $SDK_TAR_PATH"

ensure_cached_archive \
  "$ANDROID_NDK_URL" \
  "$NDK_ZIP_PATH" \
  sha1 \
  "$ANDROID_NDK_SHA1" \
  "Using cached Darwin Android NDK archive at $NDK_ZIP_PATH"

ensure_cached_archive \
  "$SKIP_MACOS_ZIP_URL" \
  "$SKIP_MACOS_ZIP_PATH" \
  sha256 \
  "$SKIP_MACOS_ZIP_SHA256" \
  "Using cached Skip macOS binary zip at $SKIP_MACOS_ZIP_PATH"
seed_swiftpm_binary_artifact \
  "$SKIP_MACOS_ZIP_PATH" \
  "$RUNNER_SWIFTPM_CACHE" \
  "$SKIP_MACOS_ZIP_URL" \
  sha256 \
  "$SKIP_MACOS_ZIP_SHA256"
seed_swiftpm_binary_artifact \
  "$SKIP_MACOS_ZIP_PATH" \
  "$RUNNER_SWIFTPM_CACHE" \
  "$SKIP_MACOS_GITHUB_ZIP_URL" \
  sha256 \
  "$SKIP_MACOS_ZIP_SHA256"
require_swiftpm_binary_artifact "$RUNNER_SWIFTPM_CACHE" "$SKIP_MACOS_ZIP_URL"
require_swiftpm_binary_artifact "$RUNNER_SWIFTPM_CACHE" "$SKIP_MACOS_GITHUB_ZIP_URL"

skip_toolchain_files_ready() {
  [[ -x "$TOOLCHAIN_SWIFT" && -f "$SDK_INFO" && -f "$NDK_SENTINEL" ]]
}

skip_toolchain_ready() {
  skip_toolchain_files_ready && [[ -d "$NDK_SYSROOT_LIB" ]]
}

if ! skip_toolchain_ready; then
  if [[ ! -f "$SDK_INFO" ]]; then
    "$MISE_SWIFT_BIN" sdk install "$SDK_TAR_PATH" --checksum "$SWIFT_ANDROID_SDK_CHECKSUM"
  fi

  if [[ ! -x "$SDK_ROOT/scripts/setup-android-sdk.sh" ]]; then
    echo "Skip Swift Android SDK setup script is missing: $SDK_ROOT/scripts/setup-android-sdk.sh" >&2
    exit 1
  fi

  if [[ ! -f "$NDK_SENTINEL" ]]; then
    rm -rf -- "$NDK_DIR"
    unzip -qo "$NDK_ZIP_PATH" -d "$SDK_ROOT"
    if [[ ! -d "$NDK_DIR" ]]; then
      echo "Expected NDK unpack folder not found: $NDK_DIR" >&2
      exit 1
    fi
    ANDROID_NDK_HOME="$NDK_DIR" "$SDK_ROOT/scripts/setup-android-sdk.sh"
    touch -- "$NDK_SENTINEL"
  fi
fi

if [[ ! -x "$TOOLCHAIN_SWIFT" ]]; then
  echo "Skip Swift toolchain is missing or not executable: $TOOLCHAIN_SWIFT" >&2
  exit 1
fi
if [[ ! -f "$SDK_INFO" ]]; then
  echo "Skip Swift Android SDK metadata is missing: $SDK_INFO" >&2
  exit 1
fi
if [[ ! -f "$NDK_SENTINEL" ]]; then
  echo "Skip Android NDK sentinel is missing: $NDK_SENTINEL" >&2
  exit 1
fi
if [[ ! -d "$NDK_SYSROOT_LIB" ]]; then
  echo "Skip Android NDK sysroot is missing: $NDK_SYSROOT_LIB" >&2
  exit 1
fi
