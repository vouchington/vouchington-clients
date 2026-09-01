---
name: dotnet-test-authoring
description: Use when adding or changing .NET tests in dotnet-clients.
---

# .NET Test Authoring

## Canonical skill (required)

Claude Code and Codex load `vouchington-testing:dotnet-test-authoring`; Grok and Cursor read
`node_modules/vouchington-tooling/skills/dotnet-test-authoring/SKILL.md`. If the canonical skill
cannot be read, stop and report the missing prerequisite; never apply this overlay alone.

## Client additions

This `vouchington-clients` overlay is intentionally limited to .NET-native policy.
Read [`dotnet-clients/CLAUDE.md`](../../../dotnet-clients/CLAUDE.md) and
[`dotnet-clients/README.md`](../../../dotnet-clients/README.md) before changing a .NET test. The
canonical skill owns portable test-authoring policy; this overlay supplies the client-specific
Core/App split, harness, coverage, native-selection, file-length, and Mac Catalyst guidance from
those documents.

Portable behavior belongs in `tests/Voucha.Client.Core.Tests`; rendered MAUI behavior belongs in
`tests/Voucha.Client.App.Tests`. Batch compatible changes in one planner invocation. Source files
have a 200-line cap and tests a 500-line cap; validate rendered changes against Mac Catalyst.

Plan changed .NET tests with `pnpm run test:plan:dotnet --base origin/main --head HEAD`; do not plan
from the Filaments checkout. Shared API fixtures and localization inputs remain Filaments-owned
and must be supplied through the explicit contract root.
