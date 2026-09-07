#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
# shellcheck source=swift-clients/tooling/cached-archive.sh
source "$SCRIPT_DIR/../../../tooling/cached-archive.sh"

SWIFT_VERSION="6.3.3"
SWIFT_TOOLCHAIN_ID="swift-${SWIFT_VERSION}-RELEASE"
SWIFT_OSX_PKG_URL="https://download.swift.org/swift-${SWIFT_VERSION}-release/xcode/${SWIFT_TOOLCHAIN_ID}/${SWIFT_TOOLCHAIN_ID}-osx.pkg"
SWIFT_OSX_PKG_SHA256="ee82e57774d6650f94aa06302435d6f44a055b9411698db8ecb85d9a3bcc91d0"
SWIFT_ANDROID_SDK_ID="${SWIFT_TOOLCHAIN_ID}_android"
SWIFT_ANDROID_SDK_URL="https://download.swift.org/swift-${SWIFT_VERSION}-release/android-sdk/${SWIFT_TOOLCHAIN_ID}/${SWIFT_ANDROID_SDK_ID}.artifactbundle.tar.gz"
SWIFT_ANDROID_SDK_CHECKSUM="d160cc3206dd1886dae3fef2337af5e25ec034692cd0ec225721c56cc69da7f5"
ANDROID_NDK_VERSION="r27d"
ANDROID_NDK_URL="https://dl.google.com/android/repository/android-ndk-${ANDROID_NDK_VERSION}-darwin.zip"
ANDROID_NDK_SHA256="e69092f9d2bfa5d1199039980a14eb91c03cc971ab5c6968fc08a8e6b84e7bb7"
SKIP_VERSION="1.9.8"
SKIP_MACOS_ZIP_URL="https://source.skip.tools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"
SKIP_MACOS_GITHUB_ZIP_URL="https://github.com/skiptools/skip/releases/download/${SKIP_VERSION}/skip-macos.zip"
SKIP_MACOS_ZIP_SHA256="b452f271deee9be0ef6211d99692f5061f190dcd5c5584914bc0472595bedbe7"

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

SWIFTLY_TMP="${RUNNER_TEMP}/swiftly-tmp"
mkdir -p -- "$SWIFTLY_TMP" "$VOUCHA_SKIP_SWIFT_HOME"
TMPDIR="$SWIFTLY_TMP"
export TMPDIR

DOWNLOAD_DIR="$VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR"
mkdir -p -- "$DOWNLOAD_DIR"

OSX_PKG_PATH="$DOWNLOAD_DIR/${SWIFT_TOOLCHAIN_ID}-osx.pkg"
SDK_TAR_PATH="$DOWNLOAD_DIR/${SWIFT_ANDROID_SDK_ID}.artifactbundle.tar.gz"
NDK_ZIP_PATH="$DOWNLOAD_DIR/android-ndk-${ANDROID_NDK_VERSION}-darwin.zip"
SKIP_MACOS_ZIP_PATH="$DOWNLOAD_DIR/skip-macos-${SKIP_VERSION}.zip"

TOOLCHAIN_DIR="$HOME/toolchains/${SWIFT_TOOLCHAIN_ID}.xctoolchain"
SDK_BUNDLE="$HOME/Library/org.swift.swiftpm/swift-sdks/${SWIFT_ANDROID_SDK_ID}.artifactbundle"
SDK_ROOT="$SDK_BUNDLE/swift-android"
NDK_DIR="$SDK_ROOT/android-ndk-${ANDROID_NDK_VERSION}"
NDK_SENTINEL="$NDK_DIR/.extraction-complete"
NDK_SYSROOT_LIB="$SDK_ROOT/ndk-sysroot/usr/lib/aarch64-linux-android"
TOOLCHAIN_SWIFT="$TOOLCHAIN_DIR/usr/bin/swift"
SDK_INFO="$SDK_BUNDLE/info.json"

ensure_cached_archive \
  "$SWIFT_OSX_PKG_URL" \
  "$OSX_PKG_PATH" \
  sha256 \
  "$SWIFT_OSX_PKG_SHA256" \
  "Using cached Swift host toolchain package at $OSX_PKG_PATH"

ensure_cached_archive \
  "$SWIFT_ANDROID_SDK_URL" \
  "$SDK_TAR_PATH" \
  sha256 \
  "$SWIFT_ANDROID_SDK_CHECKSUM" \
  "Using cached Swift Android SDK archive at $SDK_TAR_PATH"

ensure_cached_archive \
  "$ANDROID_NDK_URL" \
  "$NDK_ZIP_PATH" \
  sha256 \
  "$ANDROID_NDK_SHA256" \
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
  if [[ ! -x "$TOOLCHAIN_SWIFT" ]]; then
    pkgutil --check-signature "$OSX_PKG_PATH"
    # pkgutil --expand requires a dest that does not exist (Error 17 if it does).
    expand_parent="$(mktemp -d "$TMPDIR/swift-osx-pkg.XXXXXX")"
    trap 'rm -rf -- "$expand_parent"' EXIT
    expand_dir="$expand_parent/expanded"
    pkgutil --expand "$OSX_PKG_PATH" "$expand_dir"
    payload="$expand_dir/Payload"
    if [[ ! -f "$payload" ]]; then
      payload="$expand_dir/${SWIFT_TOOLCHAIN_ID}-osx-package.pkg/Payload"
    fi
    if [[ ! -f "$payload" ]]; then
      echo "Swift toolchain pkg Payload not found under $expand_dir" >&2
      exit 1
    fi
    rm -rf -- "$TOOLCHAIN_DIR"
    mkdir -p -- "$TOOLCHAIN_DIR"
    tar -xf "$payload" -C "$TOOLCHAIN_DIR"
    rm -rf -- "$expand_parent"
    trap - EXIT
  fi

  if [[ ! -f "$SDK_INFO" ]]; then
    swift sdk install "$SDK_TAR_PATH" --checksum "$SWIFT_ANDROID_SDK_CHECKSUM"
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
