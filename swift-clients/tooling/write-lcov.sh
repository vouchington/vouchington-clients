#!/usr/bin/env bash
set -euo pipefail

script_dir="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
repository_root="$(CDPATH='' cd -- "$script_dir/../.." && pwd)"
package_path="$1"
test_bundle_name="$2"
output_path="$3"
package_directory="$repository_root/$package_path"

[[ -d "$package_directory" ]]

single_match() {
  local description="$1"
  local matches=()
  local path
  shift
  while IFS= read -r -d '' path; do
    matches+=("$path")
  done < <(find "$@" -print0)
  if [[ "${#matches[@]}" -ne 1 ]]; then
    printf 'Expected exactly one %s, found %s.\n' "$description" "${#matches[@]}" >&2
    return 1
  fi
  printf '%s\n' "${matches[0]}"
}

profdata="$(single_match 'default.profdata profile' "$package_directory/.build" -type f -name default.profdata)"
test_bundle="$(single_match "$test_bundle_name.xctest bundle" "$package_directory/.build" -name "$test_bundle_name.xctest")"

if [[ -d "$test_bundle" ]]; then
  test_binary="$test_bundle/Contents/MacOS/$test_bundle_name"
else
  test_binary="$test_bundle"
fi
[[ -x "$test_binary" ]] || {
  printf 'Expected executable test binary at %s.\n' "$test_binary" >&2
  exit 1
}

case "$output_path" in
  /*) ;;
  *) output_path="$repository_root/$output_path" ;;
esac
mkdir -p "$(dirname "$output_path")"
xcrun llvm-cov export -format=lcov "$test_binary" -instr-profile "$profdata" \
  -ignore-filename-regex='\.build' \
  | while IFS= read -r line || [[ -n "$line" ]]; do
    source_prefix="SF:$repository_root/"
    if [[ "$line" == "$source_prefix"* ]]; then
      printf 'SF:%s\n' "${line:${#source_prefix}}"
    else
      printf '%s\n' "$line"
    fi
  done > "$output_path"
