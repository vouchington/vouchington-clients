#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"

if ! command -v mise >/dev/null 2>&1; then
  echo 'mise is required to select the pinned Swift toolchain.' >&2
  exit 1
fi

toolchain_root="$(cd "$ROOT_DIR" && mise where swift)" || {
  echo 'The repository-pinned Swift toolchain is not installed; run mise install swift.' >&2
  exit 1
}
if [[ ! -d "$toolchain_root" ]]; then
  echo 'The repository-pinned Swift toolchain is not installed; run mise install swift.' >&2
  exit 1
fi
toolchain_root="$(cd -P -- "$toolchain_root" && pwd)"

swift_bin="$(cd "$ROOT_DIR" && mise which swift)" || {
  echo 'mise could not select the repository-pinned Swift binary.' >&2
  exit 1
}
if [[ ! -x "$swift_bin" ]]; then
  echo 'The repository-pinned Swift binary is missing or not executable.' >&2
  exit 1
fi
swift_bin="$(cd -P -- "$(dirname -- "$swift_bin")" && pwd)/$(basename -- "$swift_bin")"
case "$swift_bin" in
  "$toolchain_root"/bin/swift | "$toolchain_root"/usr/bin/swift) ;;
  *)
    echo "mise selected Swift outside its pinned installation root: $swift_bin" >&2
    exit 1
    ;;
esac
swift_target="$(realpath "$swift_bin")"
case "$swift_target" in
  "$toolchain_root"/*) ;;
  *)
    echo "mise selected Swift whose target is outside its pinned installation root: $swift_target" >&2
    exit 1
    ;;
esac

swift_version="$("$swift_bin" --version | sed -n -E '1s/^(Apple )?Swift version ([^ (]+).*/\2/p')"
case "$swift_version" in
  6.4 | 6.4.0) ;;
  *)
    echo "mise selected Swift $swift_version; the repository requires Swift 6.4.0." >&2
    exit 1
    ;;
esac

case "${1:-}" in
  '') printf '%s\n' "$swift_bin" ;;
  --llvm-cov)
    llvm_cov="$toolchain_root/usr/bin/llvm-cov"
    [[ -x "$llvm_cov" ]] || {
      echo "The repository-pinned Swift installation has no executable llvm-cov at $llvm_cov." >&2
      exit 1
    }
    printf '%s\n' "$llvm_cov"
    ;;
  *)
    echo "Unsupported Swift toolchain selection: $1" >&2
    exit 1
    ;;
esac
