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
  bash "$SCRIPT_DIR/verify-android-host-swift.sh"
  mise exec swift@6.3.3 -- swift test \
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

  MISE_SWIFT_TOOLCHAIN_ROOT="$(bash "$SCRIPT_DIR/resolve-mise-swift-toolchain.sh")"
  SKIP_SWIFT_TOOLCHAIN="$HOME/toolchains/swift-6.4.0-RELEASE.xctoolchain"
  if [[ -e "$SKIP_SWIFT_TOOLCHAIN" || -L "$SKIP_SWIFT_TOOLCHAIN" ]]; then
    if [[ ! -L "$SKIP_SWIFT_TOOLCHAIN" || \
      "$(CDPATH='' cd -- "$SKIP_SWIFT_TOOLCHAIN" && pwd -P)" != "$MISE_SWIFT_TOOLCHAIN_ROOT" ]]; then
      echo "Skip toolchain alias must point to the mise-managed Swift install: $SKIP_SWIFT_TOOLCHAIN" >&2
      exit 1
    fi
  fi

  SWIFT_TMP="${RUNNER_TEMP}/swift-tmp"
  mkdir -p -- "$SWIFT_TMP"
  TMPDIR="$SWIFT_TMP"
  export TMPDIR

  SKIP_SWIFT_SDK_INFO="$HOME/Library/org.swift.swiftpm/swift-sdks/swift-6.4.0-RELEASE_android.artifactbundle/info.json"
  SKIP_NDK_SENTINEL="$HOME/Library/org.swift.swiftpm/swift-sdks/swift-6.4.0-RELEASE_android.artifactbundle/swift-android/android-ndk-r30/.extraction-complete"
  SKIP_NDK_SYSROOT="$HOME/Library/org.swift.swiftpm/swift-sdks/swift-6.4.0-RELEASE_android.artifactbundle/swift-android/ndk-sysroot/usr/lib/aarch64-linux-android"
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
  # The helper is resolved from this script's runtime directory.
  # shellcheck source=expose-job-scoped-swift-toolchain.sh
  # shellcheck disable=SC1091
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

gradle_started_at=$SECONDS
(
  cd "$ANDROID_PROJECT_DIR"
  # Keep this explicitly scoped to the app closure even if a future Skip generator changes the
  # root project graph; settings.gradle.kts separately rejects any duplicate root modules.
  ./gradlew :app:assembleDebug
)
gradle_elapsed_seconds=$((SECONDS - gradle_started_at))
echo "Android Gradle :app:assembleDebug completed in ${gradle_elapsed_seconds}s"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    printf '### Android Gradle timing\n\n'
    printf '| Command | Elapsed |\n'
    printf '| --- | ---: |\n'
    # The backticked task name is intentionally literal Markdown.
    # shellcheck disable=SC2016
    printf '| `:app:assembleDebug` | %ss |\n' "$gradle_elapsed_seconds"
  } >> "$GITHUB_STEP_SUMMARY" || printf 'Warning: unable to write GitHub step summary: %s\n' "$GITHUB_STEP_SUMMARY" >&2
fi

skip android test \
  --package-path "$ANDROID_PACKAGE_DIR" \
  --build-test-libs "$TEST_LIBRARIES_DIR"
