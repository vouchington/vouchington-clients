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
