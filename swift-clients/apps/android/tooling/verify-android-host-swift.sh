#!/usr/bin/env bash
set -euo pipefail

expected_version="6.3.3"
if [[ "$(uname -s)" != Darwin ]]; then
  echo "Android host SwiftPM tests require macOS with Xcode Swift $expected_version." >&2
  exit 1
fi

if ! command -v xcrun >/dev/null 2>&1; then
  echo "xcrun is required to verify the selected Xcode Swift compiler." >&2
  exit 1
fi

if [[ -z "${DEVELOPER_DIR:-}" || ! -x "$DEVELOPER_DIR/usr/bin/xcodebuild" ]]; then
  echo "Select the required Xcode and set DEVELOPER_DIR before Android host SwiftPM tests." >&2
  exit 1
fi

selected_xcode_version="$("$DEVELOPER_DIR/usr/bin/xcodebuild" -version | sed -n -E '1s/^Xcode ([^ ]+).*/\1/p')"
if [[ ! "$selected_xcode_version" =~ ^26\.6(\.|$) ]]; then
  echo "Android host SwiftPM tests require selected Xcode 26.6; found ${selected_xcode_version:-unknown}." >&2
  exit 1
fi

selected_swift_path="$(xcrun --find swift)"
case "$selected_swift_path" in
  "$DEVELOPER_DIR"/*) ;;
  *)
    echo "xcrun Swift is outside selected Xcode $DEVELOPER_DIR: ${selected_swift_path:-unknown}." >&2
    exit 1
    ;;
esac
selected_swift_version="$("$selected_swift_path" --version | sed -n -E '1s/^(Apple )?Swift version ([^ (]+).*/\2/p')"
if [[ "$selected_swift_version" != "$expected_version" ]]; then
  echo "Android host SwiftPM tests require Xcode Swift $expected_version; found ${selected_swift_version:-unknown}." >&2
  exit 1
fi

mise install "swift@$expected_version"
selected_mise_version="$(mise exec "swift@$expected_version" -- swift --version | sed -n -E '1s/^(Apple )?Swift version ([^ (]+).*/\2/p')"
if [[ "$selected_mise_version" != "$expected_version" ]]; then
  echo "mise selected Swift ${selected_mise_version:-unknown}; expected $expected_version for Android host tests." >&2
  exit 1
fi

printf 'Android host SwiftPM compiler verified: Xcode Swift %s and mise Swift %s.\n' \
  "$selected_swift_version" "$selected_mise_version"
