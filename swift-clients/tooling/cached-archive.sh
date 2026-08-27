#!/usr/bin/env bash
# Functions-only helper. Sourcing this file must not download or mutate archives.

digest_file() {
  local algorithm="$1"
  local file="$2"
  if [[ ! -f "$file" ]]; then
    echo "Cannot digest missing file: $file" >&2
    return 1
  fi
  if [[ "$algorithm" == "sha256" ]] && command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{print $1}'
  elif [[ "$algorithm" == "sha1" ]] && command -v sha1sum >/dev/null 2>&1; then
    sha1sum "$file" | awk '{print $1}'
  else
    shasum -a "${algorithm#sha}" "$file" | awk '{print $1}'
  fi
}

verify_digest() {
  local algorithm="$1"
  local file="$2"
  local expected="$3"
  local actual
  actual="$(digest_file "$algorithm" "$file")"
  if [[ "$actual" != "$expected" ]]; then
    echo "$algorithm checksum mismatch for $file" >&2
    echo "Expected: $expected" >&2
    echo "Actual:   $actual" >&2
    exit 1
  fi
}

# On a persistent, shared download dir, a stale non-file entry at dest_path (e.g. a leftover
# directory from a prior bad run) would make `mv tmp_path dest_path` succeed by moving tmp_path
# *inside* it instead of replacing it, silently defeating the dest-file cache check.
publish_archive() {
  local tmp_path="$1"
  local dest_path="$2"
  if [[ -e "$dest_path" && ! -f "$dest_path" ]]; then
    echo "Removing non-file entry at $dest_path before publishing the verified download." >&2
    rm -rf -- "$dest_path"
  fi
  mv -- "$tmp_path" "$dest_path"
}

cached_archive_valid() {
  local dest_path="$1"
  local algorithm="$2"
  local expected="$3"
  [[ -f "$dest_path" ]] || return 1
  [[ "$(digest_file "$algorithm" "$dest_path")" == "$expected" ]]
}

# The download dir is shared, persistent host state, so a concurrent fetch must not collide on the
# partial's name. mktemp gives each process a unique partial; the EXIT trap is scoped to this
# subshell so it only ever removes this process's own temp.
fetch_cached_archive() {
  local url="$1"
  local dest_path="$2"
  local algorithm="$3"
  local expected="$4"
  (
    tmp_path="$(mktemp "$dest_path.partial.XXXXXX")"
    trap 'rm -f "$tmp_path"' EXIT
    curl -fL --http1.1 --retry 3 --retry-all-errors --retry-delay 1 --output "$tmp_path" "$url"
    verify_digest "$algorithm" "$tmp_path" "$expected"
    publish_archive "$tmp_path" "$dest_path"
  )
}

ensure_cached_archive() {
  local url="$1"
  local dest_path="$2"
  local algorithm="$3"
  local expected="$4"
  local hit_message="$5"
  if cached_archive_valid "$dest_path" "$algorithm" "$expected"; then
    echo "$hit_message"
    return
  fi
  if [[ -f "$dest_path" ]]; then
    rm -f "$dest_path"
  fi
  fetch_cached_archive "$url" "$dest_path" "$algorithm" "$expected"
}

# SwiftPM BinaryArtifactsManager names shared-cache files with
# url.spm_mangledToC99ExtendedIdentifier(): keep [A-Za-z0-9_], replace the rest
# with `_`. First character cannot be a digit.
spm_mangle_c99_identifier() {
  local input="$1"
  local result=""
  local index=0
  local char
  local length="${#input}"
  while [[ "$index" -lt "$length" ]]; do
    char="${input:index:1}"
    if [[ "$char" == [[:alpha:]_] ]] || { [[ "$index" -gt 0 && "$char" == [[:digit:]] ]]; }; then
      result+="$char"
    else
      result+="_"
    fi
    index=$((index + 1))
  done
  printf '%s\n' "$result"
}

seed_swiftpm_binary_artifact() {
  local source_path="$1"
  local cache_root="$2"
  local url="$3"
  local algorithm="$4"
  local expected="$5"
  local dest_path
  dest_path="$cache_root/artifacts/$(spm_mangle_c99_identifier "$url")"
  mkdir -p -- "$(dirname -- "$dest_path")"
  if cached_archive_valid "$dest_path" "$algorithm" "$expected"; then
    echo "Using cached SwiftPM binary artifact at $dest_path"
    return
  fi
  if [[ -e "$dest_path" ]]; then
    rm -rf -- "$dest_path"
  fi
  (
    tmp_path="$(mktemp "$dest_path.partial.XXXXXX")"
    trap 'rm -f "$tmp_path"' EXIT
    cp -- "$source_path" "$tmp_path"
    verify_digest "$algorithm" "$tmp_path" "$expected"
    publish_archive "$tmp_path" "$dest_path"
  )
}

require_swiftpm_binary_artifact() {
  local cache_root="$1"
  local url="$2"
  local dest_path
  dest_path="$cache_root/artifacts/$(spm_mangle_c99_identifier "$url")"
  if [[ ! -f "$dest_path" ]]; then
    echo "SwiftPM binary artifact missing after prefetch: $dest_path" >&2
    exit 1
  fi
}
