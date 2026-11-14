#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

bash "$SCRIPT_DIR/verify-ui-host-swift.sh"
cd "$ROOT_DIR"
exec bash "$SCRIPT_DIR/with-build-lock.sh" xcrun swift test \
  --package-path "$ROOT_DIR/swift-clients/ui" \
  --force-resolved-versions \
  "$@"
