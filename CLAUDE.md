# Voucha native clients

Work from this repository root. Native client source has its own scoped instructions in
`swift-clients/CLAUDE.md` and `dotnet-clients/CLAUDE.md`; read them before changing either tree.

## Cross-repository contracts

Only `contracts/filaments.json` may describe inputs from the private Filaments repository. Keep
the allowlist narrow, use `pnpm run contracts:sync` to update tracked native-localization outputs,
and use `pnpm run contracts:check` as the non-mutating parity gate.
`VOUCHA_FILAMENTS_CONTRACT_ROOT` is required and must explicitly name a complete Filaments
checkout; do not discover a sibling checkout, fetch independently, or add a fallback source.

Changes that affect shared API fixtures or native-localization must land with the corresponding
Filaments contract change, or remain draft and explicitly dependency-blocked until it does.

## Quality and changed-file tests

Run `pnpm run quality:check` for repository rules. Plan native tests with
`pnpm run test:plan:swift --base origin/main --head HEAD` and
`pnpm run test:plan:dotnet --base origin/main --head HEAD`; do not plan native tests from the
Filaments checkout. See [native quality and test planning](docs/development/native-quality.md).

Use `pnpm clean` to remove checkout-local native build, coverage, and test output. Recycle only a
linked disposable worktree with `./dev/reset-worktree`; it refuses dirty state unless `--force` is
explicit and must never be used from the primary worktree.

## Agent Blackboard

Use the upstream `agent-blackboard` plugin together with the `vouchington-workflow:blackboard`
skill for session journaling. Session ids, agent/version identities, and parent-session ids must
be explicit; never infer or generate them from host state. Use only the client credential supplied
by `AGENT_BLACKBOARD_TOKEN` with `AGENT_BLACKBOARD_URL`; fail closed when either credential is
missing, malformed, or unavailable, and never substitute an admin credential.
