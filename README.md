# Voucha native clients

Use `pnpm clean` (or `./dev/clean`) to remove Swift, Xcode, .NET, coverage, and test build output without removing dependencies or credentials. In a linked disposable worktree, `./dev/reset-worktree` returns the checkout to a fresh `origin/main` branch; it refuses dirty state unless `--force` is explicit and never runs in the primary worktree.

This repository contains the Swift and .NET native clients for Voucha.

The Node tooling needs Node.js 26 or later and any pnpm 12 release on `PATH`. pnpm is deliberately
unpinned; see [pnpm](docs/development/ci-runners.md#pnpm).

## Vouchington contracts

The native clients consume a deliberately narrow contract from
[`vouchington/vouchington`](https://github.com/vouchington/vouchington). The source-path
allowlist in [`contracts/filaments.json`](contracts/filaments.json) is the only cross-repository input:
`api-fixtures/v1` and the generated Swift and .NET localization trees. When Vouchington provides its
native-localization exporter, the trusted producer uses it in preference to any legacy generated
trees; this keeps the staged localization contract aligned during extraction. Older producer
revisions without that exporter fall back to their generated trees.

`VOUCHA_FILAMENTS_CONTRACT_ROOT` is required for every command and explicitly identifies a full
Vouchington checkout. It is never inferred from an ignored workspace or nearby checkout.

First stage the contract with the trusted Vouchington exporter and the candidate client checkout,
then use that isolated stage for synchronization and checking:

```sh
stage_root="$(mktemp -d)"
node scripts/stage-native-contract.mjs \
  --filaments-root /path/to/vouchington \
  --output-root "$stage_root" \
  --consumer-root "$PWD"
VOUCHA_FILAMENTS_CONTRACT_ROOT="$stage_root" pnpm run contracts:sync
VOUCHA_FILAMENTS_CONTRACT_ROOT="$stage_root" pnpm run contracts:check
```

Do not point `contracts:sync` directly at Vouchington after extraction: its checked-in generated
trees can predate the client-owned product sources. `contracts:check` never writes; it validates
the explicit staged root and checks that both generated outputs are exact byte-for-byte matches.
CI obtains a normal full Vouchington checkout and runs that assertion; there is no duplicated
localization bundle or sparse-checkout path.

## Native quality

The client repository owns Swift/.NET quality rules, changed-file test planning, and native test
authoring guidance. See
[`docs/development/native-quality.md`](docs/development/native-quality.md) for the local commands
and the explicit Vouchington producer boundaries.

## CI runners

This repository uses GitHub-hosted ephemeral runners. The closed label set is documented in
[`docs/development/ci-runners.md`](docs/development/ci-runners.md). Which native tests run on
Linux vs macOS, and when dual-OS is required, is documented in
[`docs/development/native-ci-test-placement.md`](docs/development/native-ci-test-placement.md).

## License

Voucha is source-available under the
[Functional Source License, Version 1.1, MIT Future License](LICENSE)
(`FSL-1.1-MIT`), the same terms as
[`vouchington/vouchington`](https://github.com/vouchington/vouchington). You may read, run, modify,
and redistribute the source for any purpose other than a Competing Use — broadly, offering it to
others as a commercial product or service that substitutes for Voucha. Internal use, non-commercial
education, and non-commercial research are explicitly permitted. Vendor copies that carry their own
notices, including `swift-clients/apps/android/Android/gradlew` and `gradlew.bat` (Apache-2.0), stay
under those notices and are not converted by the FSL future MIT grant.

Each version additionally becomes available under the MIT license two years after it is published.
See [LICENSE](LICENSE) for the controlling terms, and [CONTRIBUTING.md](CONTRIBUTING.md) for the
contribution policy.
