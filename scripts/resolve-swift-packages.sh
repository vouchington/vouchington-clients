#!/usr/bin/env bash
# Resolve the pinned SwiftPM graph for one generated Xcode project into a clones directory.
# With a restored clones cache this is an offline no-op. On a cache miss SwiftPM clones from
# github.com; only transport failures (DNS, connect, reset) are retried, every other failure
# (wrong pin, unresolved version, manifest error) fails immediately and unchanged.
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: resolve-swift-packages.sh PROJECT CLONES_DIR" >&2
  exit 2
fi
project="$1"
clones="$2"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT

transport_error='Could not resolve host|Failed to connect|Connection (reset|refused|timed out)|Operation timed out|Network is unreachable|TLS connect error|SSL_ERROR_SYSCALL|early EOF|RPC failed'

for attempt in 1 2 3; do
  if xcodebuild -resolvePackageDependencies -project "$project" -scheme Voucha \
    -clonedSourcePackagesDirPath "$clones" -onlyUsePackageVersionsFromResolvedFile \
    -disablePackageRepositoryCache > "$log" 2>&1; then
    cat "$log"
    exit 0
  fi
  cat "$log"
  if ! grep -Eq "$transport_error" "$log"; then
    echo "::error::Swift package resolution failed for a non-network reason" >&2
    exit 1
  fi
  echo "::warning::Swift package resolution hit a network failure (attempt $attempt of 3)" >&2
  # A failed clone can leave a partial bare repository that would poison the next attempt.
  rm -rf "$clones/repositories"
  sleep $((attempt * 15))
done
echo "::error::Swift package resolution failed after 3 network attempts" >&2
exit 1
