# Voucha native clients

Work from this repository root. Native client source has its own scoped instructions in
`swift-clients/AGENTS.md` and `dotnet-clients/AGENTS.md`; read them before changing either tree.

## Cross-repository contracts

Only `contracts/vouchington.json` may describe inputs from `vouchington/vouchington`: a full
40-character `commit` pin plus a narrow path allowlist. Never track a branch.

- `pnpm run contracts:pull` is the only way to obtain the contract. Without
  `VOUCHA_CONTRACT_ROOT` it shallow-fetches exactly the pinned commit into the gitignored,
  SHA-keyed `.cache/vouchington/<sha>`, runs the producer exporter, writes the generated native
  localization, and prints the resolved root. With `VOUCHA_CONTRACT_ROOT` set it uses that explicit
  checkout (or staged root) as-is, to develop against an unmerged producer pull request.
- Generated native localization is never committed. Run `contracts:pull` before any Swift, Xcode,
  or .NET build or test, and export the printed root as `VOUCHA_CONTRACT_ROOT` for fixture tests.
- `pnpm run contracts:check` is the non-mutating parity gate (pinned producer versus this checkout).
- Do not discover sibling checkouts, fetch from any other source, or add fallbacks; fail closed on
  a wrong SHA or missing path.

A producer change that affects shared API fixtures or native localization lands in Vouchington
first; the paired client change is a one-line `commit` bump to that merged main commit. Until it
merges, keep the client pull request draft and dependency-blocked (CI rejects a pin that is not
reachable from producer `main`).

## Quality and changed-file tests

Run `pnpm run quality:check` for repository rules. Plan native tests with
`pnpm run test:plan:swift --base origin/main --head HEAD` and
`pnpm run test:plan:dotnet --base origin/main --head HEAD`; do not plan native tests from the
Vouchington checkout. See [native quality and test planning](docs/development/native-quality.md).

CI runs each test on the cheapest capable runner. Portable .NET is Linux-only. Swift core runs on
Linux and macOS because Darwin `URLSession` and Linux `FoundationNetworking` are different
runtimes. Do not add an OS matrix for confidence. See
[native CI test placement](docs/development/native-ci-test-placement.md).

Use `pnpm clean` to remove checkout-local native build, coverage, and test output. Recycle only a
linked disposable worktree with `./dev/reset-worktree`; it refuses dirty state unless `--force` is
explicit and must never be used from the primary worktree.

## Agent Blackboard

Use the machine-registered `vouchington-tooling` MCP server, installed by vouchington-machines,
together with the `vouchington-workflow:blackboard` skill for session journaling. No repository
file registers an MCP server, plugin, marketplace, or MCP tool approval. Session ids, agent/version
identities, and parent-session ids must be explicit; never infer or generate them from host state.
Use only the client credential supplied by `AGENT_BLACKBOARD_TOKEN` with `AGENT_BLACKBOARD_URL`;
fail closed when either credential is missing, malformed, or unavailable, and never substitute an
admin credential.
