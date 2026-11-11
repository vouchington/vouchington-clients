# Voucha native clients

Use `pnpm clean` (or `./dev/clean`) to remove Swift, Xcode, .NET, coverage, and test build output without removing dependencies or credentials. In a linked disposable worktree, `./dev/reset-worktree` returns the checkout to a fresh `origin/main` branch; it refuses dirty state unless `--force` is explicit and never runs in the primary worktree.

This repository contains the Swift and .NET native clients for Voucha.

The Node tooling needs Node.js 26 or later and any pnpm 12 release on `PATH`. Running `pnpm test`
also needs `jq` on `PATH` for the workflow predicate tests. pnpm is deliberately unpinned; see
[pnpm](docs/development/ci-runners.md#pnpm).
For a Linux coding agent, use the [Linux setup and portable test guide](docs/development/linux-agent-setup.md).

## Vouchington contracts

The native clients consume a deliberately narrow contract from
[`vouchington/vouchington`](https://github.com/vouchington/vouchington). The allowlist and the
pinned producer `commit` in [`contracts/vouchington.json`](contracts/vouchington.json) are the only
cross-repository input: `api-fixtures/v1`, `api-contracts/v1`, and the generated Swift and .NET
localization trees. Generated localization is not committed; pull it before building or testing:

```sh
pnpm install --frozen-lockfile
export VOUCHA_CONTRACT_ROOT="$(pnpm run --silent contracts:pull)"
pnpm run contracts:check   # optional, non-mutating parity gate
```

`contracts:pull` shallow-fetches exactly the pinned commit into the gitignored
`.cache/vouchington/<sha>`, runs the producer exporter against this checkout, writes the
localization, and prints the resolved root. To develop against an unmerged Vouchington pull
request, set `VOUCHA_CONTRACT_ROOT` to a checkout of that branch before running it; the explicit
root is used as-is and nothing is fetched. To adopt a merged producer change, bump `commit` to the
new Vouchington `main` SHA. See [`docs/contracts.md`](docs/contracts.md) for the full rules, the
pin-bump workflow, and CI behavior.

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
