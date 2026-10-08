#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

usage() { echo "Usage: $0 <platform>  (e.g. macOS or iOS)"; exit 1; }
[[ $# -lt 1 ]] && usage

PLATFORM="${1}"
PROJECT_YAML="apps/${PLATFORM}/project.yml"

if [[ ! -f "$PROJECT_YAML" ]]; then
    echo "Error: $PROJECT_YAML not found"
    exit 1
fi

LOCAL_XCCONFIG="apps/Voucha.local.xcconfig"

# Preserve a developer's ignored worktree-local backend URL here. Clean clones
# and CI get the production default until the developer configures this file.
if [[ ! -f "$LOCAL_XCCONFIG" ]]; then
    dollar='$'
    printf '%s\n' \
        'VOUCHA_URL_SLASH = /' \
        "VOUCHA_API_BASE_URL = https:${dollar}(VOUCHA_URL_SLASH)${dollar}(VOUCHA_URL_SLASH)voucha.ai" \
        > "$LOCAL_XCCONFIG"
fi

echo "Generating Xcode project for ${PLATFORM}..."
xcodegen generate --spec "$PROJECT_YAML" --project "apps/${PLATFORM}"
LOCK_DIR="apps/${PLATFORM}/Voucha.xcodeproj/project.xcworkspace/xcshareddata/swiftpm"
mkdir -p "$LOCK_DIR"
cp "ui/Package.resolved" "$LOCK_DIR/Package.resolved"
echo "Generated apps/${PLATFORM}/Voucha.xcodeproj"
echo "Open with: open apps/${PLATFORM}/Voucha.xcodeproj"
