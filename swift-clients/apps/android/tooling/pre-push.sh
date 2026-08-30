#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
ANDROID_PACKAGE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ANDROID_PROJECT_DIR="$ANDROID_PACKAGE_DIR/Android"
TEST_LIBRARIES_DIR="$ANDROID_PACKAGE_DIR/.build/pre-push-test-libs"
SKIPSTONE_OUTPUTS_DIR="$ANDROID_PACKAGE_DIR/.build/plugins/outputs"

if [[ -d "$SKIPSTONE_OUTPUTS_DIR" ]]; then
  while IFS= read -r -d '' source_hash; do
    rm -f -- "$source_hash"
  done < <(find "$SKIPSTONE_OUTPUTS_DIR" -type f -name '.*.sourcehash' -print0)
fi

if [[ -z "${VOUCHA_SKIP_ANDROID_HOST_SWIFT_TEST:-}" ]]; then
  swift test \
    --package-path "$ANDROID_PACKAGE_DIR" \
    --force-resolved-versions \
    --disable-dependency-cache \
    --disable-keychain \
    --disable-netrc
fi

SKIP_ARTIFACT_BIN_DIR="$ANDROID_PACKAGE_DIR/.build/artifacts/skip/skip/skip.artifactbundle/bin"
SKIP_ARTIFACT_BIN="$SKIP_ARTIFACT_BIN_DIR/skip"
if [[ ! -x "$SKIP_ARTIFACT_BIN" ]]; then
  echo "SwiftPM Skip artifact is missing or not executable: $SKIP_ARTIFACT_BIN" >&2
  exit 1
fi
PATH="$SKIP_ARTIFACT_BIN_DIR:$PATH"
export PATH

if [[ -n "${VOUCHA_SKIP_SWIFT_HOME:-}" ]]; then
  if [[ -z "${RUNNER_TEMP:-}" || "$VOUCHA_SKIP_SWIFT_HOME" != "$RUNNER_TEMP"/* ]]; then
    echo "VOUCHA_SKIP_SWIFT_HOME must be a child of RUNNER_TEMP." >&2
    exit 1
  fi

  if [[ -z "${GRADLE_USER_HOME:-}" ]]; then
    GRADLE_USER_HOME="$HOME/.cache/voucha/gradle"
  fi
  mkdir -p -- "$GRADLE_USER_HOME"
  export GRADLE_USER_HOME

  if [[ "$(uname -s)" == "Darwin" ]]; then
    if ! gradle_cache_parent="$(getconf DARWIN_USER_TEMP_DIR 2>/dev/null)" ||
      [[ -z "$gradle_cache_parent" ]]; then
      echo "getconf DARWIN_USER_TEMP_DIR failed or returned an empty path" >&2
      exit 1
    fi
    if ! gradle_cache_parent="$(CDPATH='' cd -- "$gradle_cache_parent" && pwd -P)"; then
      echo "DARWIN_USER_TEMP_DIR must identify a readable directory" >&2
      exit 1
    fi
    GRADLE_BUILD_CACHE_DIR="$gradle_cache_parent/gradle-build-cache"
    if [[ -L "$GRADLE_BUILD_CACHE_DIR" ]]; then
      echo "GRADLE_BUILD_CACHE_DIR must not be a symlink: $GRADLE_BUILD_CACHE_DIR" >&2
      exit 1
    fi
    (umask 077 && mkdir -p -- "$GRADLE_BUILD_CACHE_DIR")
    if [[ -L "$GRADLE_BUILD_CACHE_DIR" || ! -d "$GRADLE_BUILD_CACHE_DIR" ]]; then
      echo "GRADLE_BUILD_CACHE_DIR must be a real directory: $GRADLE_BUILD_CACHE_DIR" >&2
      exit 1
    fi
    chmod 0700 "$GRADLE_BUILD_CACHE_DIR"
    export GRADLE_BUILD_CACHE_DIR
  fi

  runner_swiftpm_cache="$HOME/Library/Caches/org.swift.swiftpm"
  mkdir -p -- "$runner_swiftpm_cache"

  mkdir -p -- "$VOUCHA_SKIP_SWIFT_HOME"
  HOME="$VOUCHA_SKIP_SWIFT_HOME"
  # Foundation's homeDirectoryForCurrentUser ignores a shell-only HOME override
  # on macOS. SwiftPM uses that API for its SDK store, so keep both aligned with
  # the job-scoped home or the SDK leaks into the persistent runner account.
  CFFIXED_USER_HOME="$VOUCHA_SKIP_SWIFT_HOME"
  export HOME CFFIXED_USER_HOME
  job_swiftpm_cache="$HOME/Library/Caches/org.swift.swiftpm"
  mkdir -p -- "$(dirname -- "$job_swiftpm_cache")"
  rm -rf -- "$job_swiftpm_cache"
  ln -s -- "$runner_swiftpm_cache" "$job_swiftpm_cache"

  if [[ "${SWIFTLY_HOME_DIR:-}" != "$HOME/.swiftly" || \
    "${SWIFTLY_TOOLCHAINS_DIR:-}" != "$HOME/toolchains" ]]; then
    echo "Swiftly state and toolchains must remain inside VOUCHA_SKIP_SWIFT_HOME." >&2
    exit 1
  fi
  if [[ -z "${SWIFTLY_BIN_DIR:-}" || -z "${RUNNER_TEMP:-}" || \
    "$SWIFTLY_BIN_DIR" != "$RUNNER_TEMP"/* || ! -x "$SWIFTLY_BIN_DIR/swiftly" ]]; then
    echo "Pinned Swiftly must be executable from a job-scoped RUNNER_TEMP directory." >&2
    exit 1
  fi
  SWIFTLY_EXECUTABLE="$(command -v swiftly || true)"
  if [[ "$SWIFTLY_EXECUTABLE" != "$SWIFTLY_BIN_DIR/swiftly" ]]; then
    echo "Pinned Swiftly is unavailable at the expected PATH location." >&2
    exit 1
  fi
  if [[ "$(swiftly --version)" != "1.1.3" ]]; then
    echo "Pinned Swiftly 1.1.3 is required." >&2
    exit 1
  fi

  SWIFTLY_TMP="${RUNNER_TEMP}/swiftly-tmp"
  mkdir -p -- "$SWIFTLY_TMP"
  TMPDIR="$SWIFTLY_TMP"
  export TMPDIR

  SKIP_SWIFT_TOOLCHAIN="$HOME/toolchains/swift-6.3.3-RELEASE.xctoolchain"
  SKIP_SWIFT_SDK_INFO="$HOME/Library/org.swift.swiftpm/swift-sdks/swift-6.3.3-RELEASE_android.artifactbundle/info.json"
  SKIP_NDK_SENTINEL="$HOME/Library/org.swift.swiftpm/swift-sdks/swift-6.3.3-RELEASE_android.artifactbundle/swift-android/android-ndk-r27d/.extraction-complete"
  SKIP_NDK_SYSROOT="$HOME/Library/org.swift.swiftpm/swift-sdks/swift-6.3.3-RELEASE_android.artifactbundle/swift-android/ndk-sysroot/usr/lib/aarch64-linux-android"
  if [[ ! -x "$SKIP_SWIFT_TOOLCHAIN/usr/bin/swift" || \
    ! -f "$SKIP_SWIFT_SDK_INFO" || \
    ! -f "$SKIP_NDK_SENTINEL" || \
    ! -d "$SKIP_NDK_SYSROOT" ]]; then
    bash "$SCRIPT_DIR/materialize-skip-sdk.sh"
  fi
  if [[ ! -x "$SKIP_SWIFT_TOOLCHAIN/usr/bin/swift" ]]; then
    echo "Skip Swift toolchain is missing or not executable: $SKIP_SWIFT_TOOLCHAIN/usr/bin/swift" >&2
    exit 1
  fi
  if [[ ! -f "$SKIP_SWIFT_SDK_INFO" ]]; then
    echo "Skip Swift Android SDK metadata is missing: $SKIP_SWIFT_SDK_INFO" >&2
    exit 1
  fi
  if [[ ! -f "$SKIP_NDK_SENTINEL" ]]; then
    echo "Skip Android NDK sentinel is missing: $SKIP_NDK_SENTINEL" >&2
    exit 1
  fi
  if [[ ! -d "$SKIP_NDK_SYSROOT" ]]; then
    echo "Skip Android NDK sysroot is missing: $SKIP_NDK_SYSROOT" >&2
    exit 1
  fi

  # Skip's Gradle bridge discovers host toolchains only through the conventional
  # macOS path. Keep that path as a real directory and link only the verified
  # .xctoolchain — Foundation rejects a directory-level symlink (NSPOSIX 20).
  # shellcheck source=expose-job-scoped-swift-toolchain.sh
  source "$SCRIPT_DIR/expose-job-scoped-swift-toolchain.sh"
fi

if ! java -version >/dev/null 2>&1 && command -v brew >/dev/null 2>&1; then
  JAVA_HOME="$(brew --prefix openjdk)/libexec/openjdk.jdk/Contents/Home"
  export JAVA_HOME
fi

if [[ -z "${ANDROID_HOME:-}" && -z "${ANDROID_SDK_ROOT:-}" && -d "$HOME/Library/Android/sdk" ]]; then
  ANDROID_HOME="$HOME/Library/Android/sdk"
  export ANDROID_HOME
fi

(
  cd "$ANDROID_PROJECT_DIR"
  # Build only the app dependency closure. An unqualified task also assembles Skip's duplicate,
  # unconsumed top-level module tree.
  ./gradlew :app:assembleDebug
)

skip android test \
  --package-path "$ANDROID_PACKAGE_DIR" \
  --build-test-libs "$TEST_LIBRARIES_DIR"
