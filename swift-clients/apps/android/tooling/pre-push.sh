#!/usr/bin/env bash
set -euo pipefail

phase=all
if [[ "$#" -gt 0 ]]; then
  if [[ "$#" -ne 2 || "$1" != --phase ]]; then
    echo "Usage: $0 [--phase app|test-libs|all|app-native-dependency|app-native-producer|app-kotlin-libraries|app-consumer|test-runner|test-libs-consumer]" >&2
    exit 64
  fi
  phase="$2"
fi
case "$phase" in
  app | test-libs | all | app-native-dependency | app-native-producer | app-kotlin-libraries | app-consumer | test-runner | test-libs-consumer) ;;
  *) echo "Unknown Android validation phase: $phase" >&2; exit 64 ;;
esac

SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
ANDROID_PACKAGE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ANDROID_PROJECT_DIR="$ANDROID_PACKAGE_DIR/Android"
TEST_LIBRARIES_DIR="$ANDROID_PACKAGE_DIR/.build/pre-push-test-libs"
SKIPSTONE_OUTPUTS_DIR="$ANDROID_PACKAGE_DIR/.build/plugins/outputs"

if [[ ( "$phase" == app-consumer || "$phase" == app-kotlin-libraries || "$phase" == test-libs-consumer ) && "${VOUCHA_ANDROID_STATE_VERIFIED:-}" != 1 ]]; then
  echo 'Android consumer requires a verified exact-run generated-state handoff.' >&2
  exit 1
fi

if [[ "$phase" != app-consumer && "$phase" != test-libs-consumer && -d "$SKIPSTONE_OUTPUTS_DIR" ]]; then
  if [[ ( "$phase" != app-native-producer && "$phase" != app-kotlin-libraries ) || "${VOUCHA_ANDROID_STATE_VERIFIED:-}" != 1 ]]; then
    while IFS= read -r -d '' source_hash; do
      rm -f -- "$source_hash"
    done < <(find "$SKIPSTONE_OUTPUTS_DIR" -type f -name '.*.sourcehash' -print0)
  fi
fi

if [[ -z "${VOUCHA_SKIP_ANDROID_HOST_SWIFT_TEST:-}" ]]; then
  repository_root="$(CDPATH='' cd -- "$ANDROID_PACKAGE_DIR/../../.." && pwd)"
  host_test=(
    bash swift-clients/apps/android/tooling/run-android-host-swift-tests.sh
    --package-path swift-clients/apps/android
    --filter VouchaAndroidTests
  )
  if [[ "${GITHUB_ACTIONS:-}" == true ]]; then
    (cd -- "$repository_root" && "${host_test[@]}")
  else
    (
      cd -- "$repository_root" &&
        bash swift-clients/tooling/with-build-lock.sh "${host_test[@]}"
    )
  fi
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
    GRADLE_BUILD_CACHE_DIR="$(bash "$SCRIPT_DIR/gradle-build-cache-dir.sh")"
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

  SWIFT_TMP="${RUNNER_TEMP}/swift-tmp"
  mkdir -p -- "$SWIFT_TMP"
  TMPDIR="$SWIFT_TMP"
  export TMPDIR

  if [[ "$phase" != app-kotlin-libraries ]]; then
    MISE_SWIFT_TOOLCHAIN_ROOT="$(bash "$SCRIPT_DIR/resolve-mise-swift-toolchain.sh")"
    SKIP_SWIFT_TOOLCHAIN="$HOME/toolchains/swift-6.4.0-RELEASE.xctoolchain"
    if [[ -e "$SKIP_SWIFT_TOOLCHAIN" || -L "$SKIP_SWIFT_TOOLCHAIN" ]]; then
      if [[ ! -L "$SKIP_SWIFT_TOOLCHAIN" || \
        "$(CDPATH='' cd -- "$SKIP_SWIFT_TOOLCHAIN" && pwd -P)" != "$MISE_SWIFT_TOOLCHAIN_ROOT" ]]; then
        echo "Skip toolchain alias must point to the mise-managed Swift install: $SKIP_SWIFT_TOOLCHAIN" >&2
        exit 1
      fi
    fi

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

    # Swift Build otherwise discovers the runner's Android SDK NDK, which may be
    # older than the NDK used to build this Swift Android SDK's runtime libraries.
    ANDROID_NDK_ROOT="${SKIP_NDK_SENTINEL%/.extraction-complete}"
    ANDROID_NDK_HOME="$ANDROID_NDK_ROOT"
    export ANDROID_NDK_ROOT ANDROID_NDK_HOME

    # Skip's Gradle bridge discovers host toolchains only through the conventional
    # macOS path. Keep that path as a real directory and link only the verified
    # .xctoolchain — Foundation rejects a directory-level symlink (NSPOSIX 20).
    # The helper is resolved from this script's runtime directory.
    # shellcheck source=expose-job-scoped-swift-toolchain.sh
    # shellcheck disable=SC1091
    source "$SCRIPT_DIR/expose-job-scoped-swift-toolchain.sh"
  fi
fi

# Direct Skip/Swift phases use the pinned Swift Android SDK and explicit NDK.
# Java and the Java Android SDK are needed only by Gradle/test-library phases.
if [[ "$phase" != app-native-dependency && "$phase" != app-native-producer && "$phase" != test-runner ]]; then
  if ! java -version >/dev/null 2>&1 && command -v brew >/dev/null 2>&1; then
    JAVA_HOME="$(brew --prefix openjdk)/libexec/openjdk.jdk/Contents/Home"
    export JAVA_HOME
  fi

  if [[ -z "${ANDROID_HOME:-}" && -z "${ANDROID_SDK_ROOT:-}" && -d "$HOME/Library/Android/sdk" ]]; then
    ANDROID_HOME="$HOME/Library/Android/sdk"
    export ANDROID_HOME
  fi
fi

if [[ "$phase" == app-native-producer ]]; then
  bash "$SCRIPT_DIR/build-native-product.sh" "$ANDROID_PACKAGE_DIR"
fi

if [[ "$phase" != test-libs && "$phase" != test-runner && "$phase" != test-libs-consumer && "$phase" != app-native-dependency && "$phase" != app-native-producer ]]; then
  gradle_started_at=$SECONDS
  consumer_log=""
  gradle_tasks=(:app:assembleDebug :app:assembleDebugAndroidTest)
  if [[ "$phase" == app-consumer ]]; then
    consumer_log="${RUNNER_TEMP:?}/android-consumer-gradle.log"
  elif [[ "$phase" == app-kotlin-libraries ]]; then
    gradle_tasks=(:skipstone:SkipUI:compileDebugKotlin)
  fi
  (
    cd "$ANDROID_PROJECT_DIR"
    # Keep this explicitly scoped to the app closure even if a future Skip generator changes the
    # root project graph; settings.gradle.kts separately rejects any duplicate root modules.
    if [[ "$phase" == app-consumer ]]; then
      ./gradlew --profile "${gradle_tasks[@]}" 2>&1 | tee "$consumer_log"
      echo 'Observed native Swift Gradle task outcomes in exact-run consumer:'
      grep -E '^> Task :skipstone:VouchaAndroid:buildAndroidSwiftPackageDebug( |$)' "$consumer_log" || true
    elif [[ "$phase" == app-kotlin-libraries ]]; then
      ./gradlew --profile --init-script "$SCRIPT_DIR/kotlin-libraries-only.init.gradle" "${gradle_tasks[@]}"
    else
      ./gradlew --profile "${gradle_tasks[@]}"
    fi
  )
  gradle_elapsed_seconds=$((SECONDS - gradle_started_at))
  echo "Android Gradle ${gradle_tasks[*]} completed in ${gradle_elapsed_seconds}s"
  if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
    {
      printf '### Android Gradle timing\n\n'
      printf '| Command | Elapsed |\n'
      printf '| --- | ---: |\n'
      # The backticked task name is intentionally literal Markdown.
      # shellcheck disable=SC2016
      printf '| `%s` | %ss |\n' "${gradle_tasks[*]}" "$gradle_elapsed_seconds"
    } >> "$GITHUB_STEP_SUMMARY" || printf 'Warning: unable to write GitHub step summary: %s\n' "$GITHUB_STEP_SUMMARY" >&2
    current_profile=""
    if [[ -n "$consumer_log" ]]; then
      current_profile="$(sed -n 's@^See the profiling report at: file://@@p' "$consumer_log" | tail -n 1)"
    fi
    if ! ANDROID_GRADLE_PROFILE_PATH="$current_profile" node "$SCRIPT_DIR/report-gradle-profile.mjs" | tee -a "$GITHUB_STEP_SUMMARY"; then
      printf 'Warning: unable to summarize Android Gradle profile; build result remains authoritative.\n' >&2
    fi
  fi
fi

if [[ "$phase" == app-native-dependency ]]; then
  # Match settings.gradle.kts's prebuild invocation directly. Skip 1.9.13 runs
  # the host SwiftPM build that materializes the same native Gradle package.
  plugin_ref="$(mktemp "${RUNNER_TEMP:?}/skip-plugin-path.XXXXXX")"
  trap 'rm -f -- "$plugin_ref"' EXIT
  skip plugin --prebuild --package-path "$ANDROID_PACKAGE_DIR" --plugin-ref "$plugin_ref"
  if [[ "$(cat "$plugin_ref")" != "$ANDROID_PACKAGE_DIR/.build/Android/skip-gradle" ]]; then
    echo "Skip prebuild returned an unexpected Gradle plugin path" >&2
    exit 1
  fi
  generated_module="$ANDROID_PACKAGE_DIR/.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid"
  generated_package="$generated_module/src/main/swift"
  generated_scratch="$generated_module/build/swift"
  if [[ ! -f "$generated_package/Package.swift" ]]; then
    echo "Skip prebuild did not create the native Gradle package: $generated_package" >&2
    exit 1
  fi
  # Match :skipstone:VouchaAndroid:buildAndroidSwiftPackageDebug except for the
  # SwiftPM selection. The original Gradle task still runs in the APK phase.
  skip android build \
    -d "$generated_module/build/jni-libs" \
    --package-path "$generated_package" \
    --configuration debug \
    --target SkipSwiftUI \
    --scratch-path "$generated_scratch" \
    --arch automatic \
    -Xcc -fPIC \
    -Xswiftc -DSKIP_BRIDGE \
    -Xswiftc -DTARGET_OS_ANDROID \
    -Xswiftc -Xfrontend \
    -Xswiftc -no-clang-module-breadcrumbs \
    -Xswiftc -module-cache-path \
    -Xswiftc "$generated_scratch/module-cache/VouchaAndroid"
  # The state handoff includes this Gradle-owned directory. No Gradle task has
  # run in this stage; the original native Gradle task runs in the APK phase.
  mkdir -p -- "$ANDROID_PROJECT_DIR/.gradle"
fi

if [[ "$phase" == test-libs || "$phase" == all || "$phase" == test-runner ]]; then
  # Skip 1.9.13 still looks for the pre-Swift-6.4 XCTest executable name:
  # https://github.com/skiptools/skipstone/issues/244
  # Build the actual Android test runner first, then verify its compatibility link.
  skip android build \
    --package-path "$ANDROID_PACKAGE_DIR" \
    --arch aarch64 \
    --build-tests
fi

if [[ "$phase" == test-libs || "$phase" == all || "$phase" == test-libs-consumer ]]; then
  bash "$SCRIPT_DIR/link-swift64-android-test-runner.sh" "$ANDROID_PACKAGE_DIR"

  skip android test \
    --package-path "$ANDROID_PACKAGE_DIR" \
    --build-test-libs "$TEST_LIBRARIES_DIR"
fi
