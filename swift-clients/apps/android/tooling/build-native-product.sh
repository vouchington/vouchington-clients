#!/usr/bin/env bash
set -euo pipefail

package_dir="$(CDPATH='' cd -- "${1:?Android package path is required}" && pwd -P)"
module="$package_dir/.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid"
if [[ ! -f "$module/src/main/swift/Package.swift" || ! -f "$module/build.gradle.kts" ]]; then
  echo 'Native producer requires the generated VouchaAndroid package from the dependency handoff.' >&2
  exit 1
fi

# Pin only Skip 1.9.13's path/command resolvers and native task registration/body.
# Fail closed when generator semantics change, rather than silently diverging
# from the original Gradle task that still runs during final APK assembly.
task_digest="$(sed -n '1,9p; /^tasks.configureEach {$/,/^tasks.withType<Test> {$/p' "$module/build.gradle.kts" | shasum -a 256 | awk '{print $1}')"
if [[ "$task_digest" != 24e66c33eb758930e9ae69dade7754ecb66638fc3509556763d7c4831b052504 ]]; then
  echo 'Generated Skip native Gradle task changed; review direct invocation parity before updating its digest.' >&2
  exit 1
fi

# Match skipcmd selection from the guarded generated task, including Xcode and
# an existing SKIP_COMMAND_OVERRIDE. The CI PATH already selects pinned Skip.
cd -- "$module"
module="$(pwd -P)"
if [[ "$module" != "$package_dir/.build/plugins/outputs/"* ]]; then
  echo 'Generated native module must remain inside the admitted Skip outputs.' >&2
  exit 1
fi
skip_config="${CONFIGURATION-Debug}"
skip_command="${SKIP_COMMAND_OVERRIDE-${__XCODE_BUILT_PRODUCTS_DIR_PATHS-${BUILT_PRODUCTS_DIR-null}}/../$skip_config/skip}"
skipcmd=skip
if [[ -e "$skip_command" ]]; then
  skipcmd="$skip_command"
  export SKIP_COMMAND_OVERRIDE="$skip_command"
fi

started_at=$SECONDS
(
  cd -- "$module"
  export SKIP_BRIDGE=1 TARGET_OS_ANDROID=1 DEVELOPER_DIR=''
  "$skipcmd" android build \
    -d "$module/build/jni-libs" \
    --package-path "$module/src/main/swift" \
    --configuration debug \
    --product VouchaAndroid \
    --scratch-path "$module/build/swift" \
    --arch automatic \
    -Xcc -fPIC \
    -Xswiftc -DSKIP_BRIDGE \
    -Xswiftc -DTARGET_OS_ANDROID \
    -Xswiftc -Xfrontend \
    -Xswiftc -no-clang-module-breadcrumbs \
    -Xswiftc -module-cache-path \
    -Xswiftc "$module/build/swift/module-cache/VouchaAndroid"
)
elapsed=$((SECONDS - started_at))
echo "Android native Skip product completed in ${elapsed}s"
if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  printf '### Android native product timing\n\nDirect native Skip product: %ss\n' "$elapsed" >> "$GITHUB_STEP_SUMMARY" || printf 'Warning: unable to write GitHub step summary\n' >&2
fi
