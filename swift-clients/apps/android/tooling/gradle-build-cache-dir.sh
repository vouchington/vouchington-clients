#!/usr/bin/env bash
# Print the real Gradle task-output cache used by Android builds.
set -euo pipefail

if [[ "$(uname -s)" != Darwin ]]; then
  echo 'Gradle task-output cache selection requires macOS' >&2
  exit 1
fi
if [[ "${GITHUB_ACTIONS:-}" == true ]]; then
  # actions/cache preserves absolute tar paths. The runner's Darwin temp path is
  # randomized per host, so use a stable runner-home path for cross-run restores.
  if [[ -L "$HOME" || ! -d "$HOME" ]]; then
    echo 'GitHub runner HOME must be a real directory' >&2
    exit 1
  fi
  cache_home="$(CDPATH='' cd -- "$HOME" && pwd -P)"
  for cache_parent in "$cache_home/.cache" "$cache_home/.cache/voucha"; do
    if [[ -L "$cache_parent" || ( -e "$cache_parent" && ! -d "$cache_parent" ) ]]; then
      echo "Gradle cache parent must be a real directory: $cache_parent" >&2
      exit 1
    fi
    (umask 077 && mkdir -p -- "$cache_parent")
    if [[ -L "$cache_parent" || ! -d "$cache_parent" ]]; then
      echo "Gradle cache parent must be a real directory: $cache_parent" >&2
      exit 1
    fi
  done
else
  if ! cache_parent="$(getconf DARWIN_USER_TEMP_DIR 2>/dev/null)" || [[ -z "$cache_parent" ]]; then
    echo 'getconf DARWIN_USER_TEMP_DIR failed or returned an empty path' >&2
    exit 1
  fi
  if ! cache_parent="$(CDPATH='' cd -- "$cache_parent" && pwd -P)"; then
    echo 'DARWIN_USER_TEMP_DIR must identify a readable directory' >&2
    exit 1
  fi
fi
cache_dir="$cache_parent/gradle-build-cache"
if [[ -L "$cache_dir" ]]; then
  echo "GRADLE_BUILD_CACHE_DIR must not be a symlink: $cache_dir" >&2
  exit 1
fi
(umask 077 && mkdir -p -- "$cache_dir")
if [[ -L "$cache_dir" || ! -d "$cache_dir" ]]; then
  echo "GRADLE_BUILD_CACHE_DIR must be a real directory: $cache_dir" >&2
  exit 1
fi
chmod 0700 "$cache_dir"
printf '%s\n' "$cache_dir"
