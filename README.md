# Voucha native clients

This repository contains the Swift and .NET native clients for Voucha.

## Filaments contracts

The native clients consume a deliberately narrow contract from the private
[`jonathanong/filaments`](https://github.com/jonathanong/filaments) repository. The source-path
allowlist in [`contracts/filaments.json`](contracts/filaments.json) is the only cross-repository input:
`api-fixtures/v1` and Filaments' existing generated Swift and .NET localization trees.

`VOUCHA_FILAMENTS_CONTRACT_ROOT` is required for every command and explicitly identifies a full
Filaments checkout. It is never inferred from an ignored workspace or nearby checkout.

Run `VOUCHA_FILAMENTS_CONTRACT_ROOT=/path/to/filaments pnpm run contracts:sync` to validate the
allowlisted source trees and replace the generated Swift and .NET localization outputs.
`contracts:check` never writes; it validates the explicit checkout root and checks that both
generated outputs are exact byte-for-byte matches. CI obtains a normal full Filaments checkout and
runs that assertion; there is no duplicated localization bundle or sparse-checkout path.
