#!/usr/bin/env bash

clean_build_output() {
  local repository_root="$1"

  rm -rf -- \
    "$repository_root/coverage" \
    "$repository_root/coverage-affected" \
    "$repository_root/coverage-full" \
    "$repository_root/coverage-html" \
    "$repository_root/playwright-report" \
    "$repository_root/test-results" \
    "$repository_root/dotnet-clients/TestResults" \
    "$repository_root/dotnet-clients/.nuget/lock-artifacts"

  find "$repository_root/swift-clients" \
    \( -path '*/node_modules' -o -path '*/.git' \) -prune -o \
    -type d \( -name .build -o -name DerivedData -o -name '*.xcodeproj' -o -name '*.xcworkspace' \) \
    -prune -exec rm -rf -- {} +

  find "$repository_root/swift-clients/apps/android" -type d \
    \( -name build -o -name .gradle -o -name .kotlin -o -name .android \) \
    -prune -exec rm -rf -- {} +

  find "$repository_root/dotnet-clients" \
    \( -path '*/node_modules' -o -path '*/.git' \) -prune -o \
    -type d \( -name bin -o -name obj -o -name TestResults -o -name artifacts \) \
    -prune -exec rm -rf -- {} +

  find "$repository_root" \
    \( -path "$repository_root/.git" -o -path "$repository_root/node_modules" -o \
       -path "$repository_root/.worktrees" \) -prune -o \
    -type d -name .cache -prune -exec rm -rf -- {} +
}
