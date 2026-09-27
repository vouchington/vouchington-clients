---
name: swift-test-authoring
description: Use when adding or changing Swift tests in swift-clients.
---

# Swift Test Authoring

## Canonical skill (required)

Claude Code and Codex load `vouchington-testing:swift-test-authoring`; Grok and Cursor read
`node_modules/vouchington-tooling/skills/swift-test-authoring/SKILL.md`. If the canonical skill
cannot be read, stop and report the missing prerequisite; never apply this overlay alone.

## Client additions

This `vouchington-clients` overlay is intentionally limited to Swift-native policy.
Read [`swift-clients/AGENTS.md`](../../../swift-clients/AGENTS.md) and
[`swift-clients/README.md`](../../../swift-clients/README.md) before changing a Swift test. The
canonical skill owns portable testing policy; this overlay supplies the client-specific harness,
coverage, ViewInspector, `URLProtocol`, file-length, and native-selection guidance from those
documents. This repository enforces a 90% Swift patch-coverage threshold and a 500-line test cap.
Delayed `URLProtocol` doubles must synchronize mutable state and guard callbacks after
`stopLoading()`.

Use the native harness and the repository's host-lock guidance for compiler-heavy checks. Plan
changed Swift tests with `pnpm run test:plan:swift --base origin/main --head HEAD`; do not plan from
the Vouchington checkout. Shared API fixtures and localization inputs remain Vouchington-owned and
must be supplied through the explicit contract root. Darwin-only tests must use
`#if canImport(Darwin)` or `#if canImport(Security)`; CI placement is
[native CI test placement](../../../docs/development/native-ci-test-placement.md).
