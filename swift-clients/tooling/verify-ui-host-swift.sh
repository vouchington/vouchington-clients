#!/usr/bin/env bash
set -euo pipefail

# SwiftUI package tests must use the selected Xcode's Swift. The pinned mise
# toolchain cannot compile the locked ViewInspector sources against the Xcode 27 SDK.

if [[ "$(uname -s)" != Darwin ]]; then
  echo 'SwiftUI package tests require macOS and the selected Xcode Swift compiler.' >&2
  exit 1
fi

fail() {
  cat >&2 <<'EOF'
SwiftUI package tests require the selected Xcode's bundled Swift compiler (xcrun swift).
The pinned mise Swift toolchain, including Swift 6.4.0, cannot compile the locked
ViewInspector sources against the Xcode 27 SDK. That compiler does not see
MapUserTrackingMode, MapInteractionModes, _MapAnnotationData,
_DefaultAnnotatedMapContent, MapAnnotation, MapMarker, MapPin,
SignInWithAppleButton, or VideoPlayer.

Select the Xcode you have for this command. Hosted test-swift-ui still uses Xcode 26.6.
  export DEVELOPER_DIR="/Applications/Xcode.app/Contents/Developer"
  xcodebuild -version
  xcrun swift --version
  bash swift-clients/tooling/test-ui.sh

Planner filters are arguments to that script:
  bash swift-clients/tooling/test-ui.sh --filter VouchaUITests

Do not run `swift test --package-path swift-clients/ui` with the mise shim.
EOF
  exit 1
}

if ! command -v xcrun >/dev/null 2>&1; then
  echo 'xcrun is required to select the Xcode Swift compiler.' >&2
  fail
fi

developer_dir="${DEVELOPER_DIR:-}"
if [[ -z "$developer_dir" ]]; then
  developer_dir="$(xcode-select -p 2>/dev/null || true)"
fi
if [[ -z "$developer_dir" || ! -x "$developer_dir/usr/bin/xcodebuild" ]]; then
  echo 'Select an installed Xcode and set DEVELOPER_DIR before SwiftUI package tests.' >&2
  fail
fi
developer_dir="$(cd -P -- "$developer_dir" && pwd)"

xcode_banner="$("$developer_dir/usr/bin/xcodebuild" -version)"
xcode_version="$(printf '%s\n' "$xcode_banner" | sed -n -E '1s/^Xcode ([^ ]+).*/\1/p')"
if [[ -z "$xcode_version" ]]; then
  echo 'The selected developer directory did not report an Xcode version.' >&2
  fail
fi

swift_path="$(DEVELOPER_DIR="$developer_dir" xcrun --find swift 2>/dev/null || true)"
if [[ -z "$swift_path" || ! -x "$swift_path" ]]; then
  echo 'xcrun could not find the Swift compiler inside the selected Xcode.' >&2
  fail
fi
swift_real="$(cd -P -- "$(dirname -- "$swift_path")" && pwd)/$(basename -- "$swift_path")"
case "$swift_real" in
  "$developer_dir"/*) ;;
  *)
    echo "xcrun Swift is outside selected Xcode $developer_dir: $swift_real" >&2
    fail
    ;;
esac

swift_version="$("$swift_real" --version 2>/dev/null | sed -n -E '1s/^(Apple )?Swift version //p')"
if [[ -z "$swift_version" ]]; then
  echo 'The selected Xcode Swift compiler did not report a version.' >&2
  fail
fi

printf 'SwiftUI host compiler verified: Xcode %s, Swift %s (%s).\n' \
  "$xcode_version" "$swift_version" "$swift_real"
