# Voucha native clients

This repository contains the Swift and .NET native clients for Voucha.

## Filaments contracts

The native clients consume a deliberately narrow contract from the private
[`jonathanong/filaments`](https://github.com/jonathanong/filaments) repository. The allowlist in
[`contracts/filaments.json`](contracts/filaments.json) is the only cross-repository input:
`api-fixtures/v1` and the native-localization bundle.

`VOUCHA_FILAMENTS_CONTRACT_ROOT` is required for every command and explicitly identifies a full
Filaments checkout. It is never inferred from an ignored workspace or nearby checkout.

Run `VOUCHA_FILAMENTS_CONTRACT_ROOT=/path/to/filaments pnpm run contracts:sync` to validate the
allowlisted source trees and replace the generated Swift and .NET localization outputs.
`contracts:check` never writes; it validates the explicit checkout root and checks that both
generated outputs are exact matches.

This foundation depends on Filaments adding
`client-contracts/v1/native-localization`. Until that change is merged, contract commands fail
closed by design; tests use a local git-style checkout fixture instead of a production fallback.
