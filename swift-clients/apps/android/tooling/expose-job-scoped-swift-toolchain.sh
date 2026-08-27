#!/usr/bin/env bash
set -euo pipefail

# Skip's Gradle bridge lists $HOME/Library/Developer/Toolchains through
# Foundation. A directory-level symlink is rejected with NSPOSIX Code 20
# (ENOTDIR), so keep that path as a real directory and expose only the
# verified .xctoolchain entry.

if [[ -z "${HOME:-}" ]]; then
  echo "HOME is required." >&2
  exit 1
fi
if [[ -z "${SKIP_SWIFT_TOOLCHAIN:-}" || ! -x "$SKIP_SWIFT_TOOLCHAIN/usr/bin/swift" ]]; then
  echo "Skip Swift toolchain is missing or not executable: ${SKIP_SWIFT_TOOLCHAIN:-}" >&2
  exit 1
fi

swift_toolchain_discovery_dir="$HOME/Library/Developer/Toolchains"
swift_toolchain_discovery_link="$swift_toolchain_discovery_dir/$(basename -- "$SKIP_SWIFT_TOOLCHAIN")"

if [[ -L "$swift_toolchain_discovery_dir" ]]; then
  echo "Swift toolchain discovery path must be a real directory, not a symlink: $swift_toolchain_discovery_dir" >&2
  exit 1
fi
if [[ -e "$swift_toolchain_discovery_dir" && ! -d "$swift_toolchain_discovery_dir" ]]; then
  echo "Swift toolchain discovery path must be a real directory, not a symlink: $swift_toolchain_discovery_dir" >&2
  exit 1
fi
mkdir -p -- "$swift_toolchain_discovery_dir"

if [[ -e "$swift_toolchain_discovery_link" && ! -L "$swift_toolchain_discovery_link" ]]; then
  echo "Swift toolchain discovery entry is not a symlink: $swift_toolchain_discovery_link" >&2
  exit 1
fi
if [[ ! -L "$swift_toolchain_discovery_link" ]]; then
  ln -s "$SKIP_SWIFT_TOOLCHAIN" "$swift_toolchain_discovery_link"
fi
if [[ "$(readlink "$swift_toolchain_discovery_link")" != "$SKIP_SWIFT_TOOLCHAIN" ]]; then
  echo "Swift toolchain discovery link does not target the verified job-scoped toolchain." >&2
  exit 1
fi
