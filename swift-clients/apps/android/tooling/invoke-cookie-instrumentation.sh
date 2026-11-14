#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  echo "Usage: $0 <instrumentation-package> <test-class-and-method>" >&2
  exit 64
fi

test_id="$1"
test_case="$2"
exec adb shell am instrument -w -r -e class "voucha.android.$test_case" \
  "$test_id/androidx.test.runner.AndroidJUnitRunner"
