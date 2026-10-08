#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

if [[ $# -ne 1 || ( "$1" != 'core' && "$1" != 'ui' ) ]]; then
  echo 'Usage: periphery-scan.sh <core|ui>' >&2
  exit 2
fi

PACKAGE="$1"
PACKAGE_DIR="$ROOT_DIR/swift-clients/$PACKAGE"
BUILD_PATH="$PACKAGE_DIR/.build/periphery-index"
SWIFT_TOOLCHAIN="${VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN:-mise}"

case "$SWIFT_TOOLCHAIN" in
  mise)
    SWIFT_COMMAND=(mise exec -- swift)
    ;;
  xcode)
    SWIFT_COMMAND=(xcrun swift)
    ;;
  *)
    echo 'Error: VOUCHA_PERIPHERY_SWIFT_TOOLCHAIN must be mise or xcode.' >&2
    exit 2
    ;;
esac

rm -rf -- "$BUILD_PATH"
(
  cd "$PACKAGE_DIR"
  "${SWIFT_COMMAND[@]}" build \
    --build-path .build/periphery-index \
    --enable-index-store \
    --build-tests \
    --force-resolved-versions \
    --disable-dependency-cache
)

BIN_PATH="$(
  cd "$PACKAGE_DIR"
  "${SWIFT_COMMAND[@]}" build --build-path .build/periphery-index --show-bin-path
)"
INDEX_STORE="$BIN_PATH/index/store"
case "$(CDPATH='' cd -- "$BIN_PATH" && pwd -P)" in
  "$(CDPATH='' cd -- "$BUILD_PATH" && pwd -P)"/*) ;;
  *)
    echo "Error: Swift build directory resolved outside the scoped build path: $BIN_PATH" >&2
    exit 1
    ;;
esac

[[ -d "$INDEX_STORE" ]] || {
  echo "Error: Swift build did not create the expected index store: $INDEX_STORE" >&2
  exit 1
}

(
  cd "$PACKAGE_DIR"
  periphery scan --strict --skip-build --index-store-path "$INDEX_STORE"
)
