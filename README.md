# Voucha native clients

This repository contains the Swift and .NET native clients for Voucha.

## Filaments contracts

The native clients consume a deliberately narrow contract from the private
[`jonathanong/filaments`](https://github.com/jonathanong/filaments) repository. The source-path
allowlist in [`contracts/filaments.json`](contracts/filaments.json) is the only cross-repository input:
`api-fixtures/v1` and the generated Swift and .NET localization trees. When Filaments provides its
native-localization exporter, the trusted producer uses it in preference to any legacy generated
trees; this keeps the staged localization contract aligned during extraction. Older producer
revisions without that exporter fall back to their generated trees.

`VOUCHA_FILAMENTS_CONTRACT_ROOT` is required for every command and explicitly identifies a full
Filaments checkout. It is never inferred from an ignored workspace or nearby checkout.

First stage the contract with the trusted Filaments exporter and the candidate client checkout,
then use that isolated stage for synchronization and checking:

```sh
stage_root="$(mktemp -d)"
node scripts/stage-native-contract.mjs \
  --filaments-root /path/to/filaments \
  --output-root "$stage_root" \
  --consumer-root "$PWD"
VOUCHA_FILAMENTS_CONTRACT_ROOT="$stage_root" pnpm run contracts:sync
VOUCHA_FILAMENTS_CONTRACT_ROOT="$stage_root" pnpm run contracts:check
```

Do not point `contracts:sync` directly at Filaments after extraction: its checked-in generated
trees can predate the client-owned product sources. `contracts:check` never writes; it validates
the explicit staged root and checks that both generated outputs are exact byte-for-byte matches.
CI obtains a normal full Filaments checkout and runs that assertion; there is no duplicated
localization bundle or sparse-checkout path.

## Native quality

The client repository owns Swift/.NET quality rules, changed-file test planning, and native test
authoring guidance. See
[`docs/development/native-quality.md`](docs/development/native-quality.md) for the local commands
and the explicit Filaments producer boundaries.

## CI runners

This private repository uses the organization self-hosted fleet to avoid consuming GitHub-hosted
minutes. Runner labels and the required persistent-runner cleanup boundary are documented in
[`docs/development/ci-runners.md`](docs/development/ci-runners.md).
