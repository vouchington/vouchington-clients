#!/bin/sh
# Fail a Release build when the Turnstile site key is missing or is a Cloudflare test key.
set -eu

if [ "$#" -ne 1 ]; then
  echo "usage: turnstile-release-site-key.sh <site-key>" >&2
  exit 2
fi

key=$(printf '%s' "$1" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
keys_file=$(CDPATH='' cd -- "$(dirname "$0")" && pwd)/turnstile-test-site-keys.txt

if [ -z "$key" ]; then
  echo "error: Release builds require an explicit non-test VOUCHA_TURNSTILE_SITE_KEY." >&2
  exit 1
fi

while IFS= read -r test_key || [ -n "$test_key" ]; do
  if [ "$key" = "$test_key" ]; then
    echo "error: Release builds reject Cloudflare Turnstile test site keys." >&2
    exit 1
  fi
done < "$keys_file"
