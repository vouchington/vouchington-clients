---
name: swift-test-authoring
description: Use when adding or changing Swift tests in swift-clients.
---

# Swift Test Authoring

Read `swift-clients/CLAUDE.md` and `swift-clients/README.md` first. This vouchington-clients repository enforces a 90% Swift patch-coverage threshold; use ViewInspector for SwiftUI views and keep tests within 500 lines. Delayed `URLProtocol` doubles must synchronize mutable state and guard callbacks after `stopLoading()`.

Filaments host-lock and test-value guidance is authoritative at https://github.com/jonathanong/filaments/blob/main/docs/development/host-locks.md and https://github.com/jonathanong/filaments/blob/main/docs/development/reference-tests-value-and-reduction.md.
