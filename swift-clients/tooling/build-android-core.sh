#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
CORE_DIR="$ROOT_DIR/swift-clients/core"
# shellcheck source=swift-clients/tooling/cached-archive.sh
source "$SCRIPT_DIR/cached-archive.sh"

SWIFT_ANDROID_SDK_VERSION="6.3.3"
SWIFT_ANDROID_SDK_ID="swift-6.3.3-RELEASE_android"
SWIFT_ANDROID_SDK_URL="https://download.swift.org/swift-6.3.3-release/android-sdk/swift-6.3.3-RELEASE/swift-6.3.3-RELEASE_android.artifactbundle.tar.gz"
SWIFT_ANDROID_SDK_CHECKSUM="d160cc3206dd1886dae3fef2337af5e25ec034692cd0ec225721c56cc69da7f5"
SWIFT_ANDROID_TARGET_TRIPLE="aarch64-unknown-linux-android28"
SWIFT_ANDROID_CACHE_ROOT="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/voucha-swift-android-sdk/${SWIFT_ANDROID_SDK_VERSION}"
SWIFT_ANDROID_DOWNLOAD_DIR="${VOUCHA_SWIFT_ANDROID_DOWNLOAD_DIR:-$SWIFT_ANDROID_CACHE_ROOT/downloads}"
SWIFT_ANDROID_DOWNLOAD_PATH="$SWIFT_ANDROID_DOWNLOAD_DIR/swift-${SWIFT_ANDROID_SDK_VERSION}-RELEASE_android.artifactbundle.tar.gz"
SWIFT_ANDROID_BUILD_SCRATCH="$SWIFT_ANDROID_CACHE_ROOT/scratch"
SWIFT_ANDROID_OFFLINE="${VOUCHA_SWIFT_ANDROID_OFFLINE:-}"
ANDROID_NDK_VERSION="r27d"
ANDROID_NDK_URL="https://dl.google.com/android/repository/android-ndk-${ANDROID_NDK_VERSION}-Linux.zip"
ANDROID_NDK_SHA1="22105e410cf29afcf163760cc95522b9fb981121"
ANDROID_NDK_PATH="$SWIFT_ANDROID_CACHE_ROOT/android-ndk-${ANDROID_NDK_VERSION}"
ANDROID_NDK_DOWNLOAD_PATH="$SWIFT_ANDROID_DOWNLOAD_DIR/android-ndk-${ANDROID_NDK_VERSION}-Linux.zip"
ANDROID_NDK_CLANG="$ANDROID_NDK_PATH/toolchains/llvm/prebuilt/linux-x86_64/bin/clang"
ANDROID_NDK_SENTINEL="$ANDROID_NDK_PATH/.extraction-complete"

# --fetch: host-side download pass only (curl available, no swift/container dependency).
# Default (no args): build pass, consuming archives pre-placed by a prior --fetch (offline-capable).
SWIFT_ANDROID_MODE="build"
if [[ "${1:-}" == "--fetch" ]]; then
  SWIFT_ANDROID_MODE="fetch"
  shift
fi

# The download dir may be a read-only bind mount during a CI offline build pass; mkdir -p is a
# no-op when it already exists, so this is safe there while still covering local/dev builds that
# never ran --fetch first.
mkdir -p "$SWIFT_ANDROID_DOWNLOAD_DIR"
if [[ "$SWIFT_ANDROID_MODE" == "build" ]]; then
  mkdir -p "$SWIFT_ANDROID_BUILD_SCRATCH"
fi

guard_offline() {
  local name="$1"
  local path="$2"
  if [[ -n "$SWIFT_ANDROID_OFFLINE" ]]; then
    echo "$name archive missing or checksum-mismatched at $path; the host prefetch step (--fetch) must populate it before an offline build runs." >&2
    exit 1
  fi
}

installed_sdk_id() {
  swift sdk list | awk -v id="$SWIFT_ANDROID_SDK_ID" '$1 == id { print $1; exit }'
}

sdk_root() {
  local swift_sdks_dir="${SWIFT_SDKS_DIR:-$HOME/.swiftpm/swift-sdks}"
  local expected="$swift_sdks_dir/${SWIFT_ANDROID_SDK_ID}.artifactbundle/swift-android"
  if [[ -x "$expected/scripts/setup-android-sdk.sh" ]]; then
    printf '%s\n' "$expected"
    return 0
  fi
  return 1
}

download_sdk() {
  if cached_archive_valid "$SWIFT_ANDROID_DOWNLOAD_PATH" sha256 "$SWIFT_ANDROID_SDK_CHECKSUM"; then
    echo "Using cached Swift Android SDK archive at $SWIFT_ANDROID_DOWNLOAD_PATH"
    return
  fi
  guard_offline "Swift Android SDK" "$SWIFT_ANDROID_DOWNLOAD_PATH"
  rm -f "$SWIFT_ANDROID_DOWNLOAD_PATH"
  fetch_cached_archive \
    "$SWIFT_ANDROID_SDK_URL" \
    "$SWIFT_ANDROID_DOWNLOAD_PATH" \
    sha256 \
    "$SWIFT_ANDROID_SDK_CHECKSUM"
}

download_ndk() {
  if cached_archive_valid "$ANDROID_NDK_DOWNLOAD_PATH" sha1 "$ANDROID_NDK_SHA1"; then
    echo "Using cached Android NDK archive at $ANDROID_NDK_DOWNLOAD_PATH"
    return
  fi
  guard_offline "Android NDK" "$ANDROID_NDK_DOWNLOAD_PATH"
  rm -f "$ANDROID_NDK_DOWNLOAD_PATH"
  fetch_cached_archive \
    "$ANDROID_NDK_URL" \
    "$ANDROID_NDK_DOWNLOAD_PATH" \
    sha1 \
    "$ANDROID_NDK_SHA1"
}

if [[ "$SWIFT_ANDROID_MODE" == "fetch" ]]; then
  download_sdk
  download_ndk
  exit 0
fi

sdk_id="$(installed_sdk_id || true)"
if [[ -z "$sdk_id" ]]; then
  download_sdk
  swift sdk install "$SWIFT_ANDROID_DOWNLOAD_PATH" --checksum "$SWIFT_ANDROID_SDK_CHECKSUM"
  sdk_id="$(installed_sdk_id)"
fi

if [[ -z "$sdk_id" ]]; then
  echo "Unable to locate the installed Swift Android SDK." >&2
  exit 1
fi

sdk_path="$(sdk_root || true)"
if [[ -z "$sdk_path" ]]; then
  echo "Unable to locate the installed Swift Android SDK root." >&2
  exit 1
fi

if [[ ! -x "$ANDROID_NDK_CLANG" || ! -f "$ANDROID_NDK_SENTINEL" ]]; then
  download_ndk
  rm -rf "$ANDROID_NDK_PATH"
  unzip -qo "$ANDROID_NDK_DOWNLOAD_PATH" -d "$SWIFT_ANDROID_CACHE_ROOT"
  touch "$ANDROID_NDK_SENTINEL"
fi

ANDROID_NDK_HOME="$ANDROID_NDK_PATH" "$sdk_path/scripts/setup-android-sdk.sh"

swift build \
  --package-path "$CORE_DIR" \
  --swift-sdk "$SWIFT_ANDROID_TARGET_TRIPLE" \
  --static-swift-stdlib \
  --scratch-path "$SWIFT_ANDROID_BUILD_SCRATCH"
