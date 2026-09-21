# Contributing

This repository holds the Swift and .NET native clients for Voucha. It is a thin front door — the
real rules live in the docs it links to; see those for details rather than looking for them
duplicated here.

**This project does not accept outside pull requests.** The source is published to be read, run,
and learned from, not to be developed collectively. Pull requests from people outside the project
are closed unreviewed. Everything below describes how work is done _inside_ the project; it is kept
public because the source is public, not as an invitation.

Bug reports and questions are welcome as issues.

## Setup

Start with the [README](README.md) for repository layout, the Filaments contract boundary, and the
`pnpm clean` / `./dev/reset-worktree` tooling. Project principles and directory-scoped rules are in
[CLAUDE.md](CLAUDE.md).

## Native quality

Swift/.NET quality rules, changed-file test planning, and native test authoring guidance live in
[`docs/development/native-quality.md`](docs/development/native-quality.md), including the explicit
Filaments producer boundaries.

`pnpm run lint:portable` is the portable gate; `pnpm run lint` also runs gitleaks and the native
harnesses under `swift-clients/tooling/` and `dotnet-clients/tooling/`. CI uses GitHub-hosted
ephemeral runners; the closed label set and the prohibition on persistent-workspace cleanup are
documented in [`docs/development/ci-runners.md`](docs/development/ci-runners.md), and the
Linux-vs-macOS test placement rules in
[`docs/development/native-ci-test-placement.md`](docs/development/native-ci-test-placement.md).

## Licensing of contributions

The repository is licensed under [FSL-1.1-MIT](LICENSE), which converts to the MIT license two
years after each version is published.

GitHub's default rule is inbound=outbound: absent any other agreement, anything you add to a
repository is licensed under that repository's own terms. Under FSL that would leave the project
unable to relicense the contribution — including at the two-year MIT conversion the license
promises everyone.

So, as an exception to that default: if you do submit a contribution despite the policy above, you
grant Jonathan Ong a perpetual, worldwide, irrevocable, royalty-free, non-exclusive license to use,
reproduce, modify, distribute, and relicense that contribution under any terms, including terms
different from FSL-1.1-MIT. You also confirm that the contribution is yours to license. This grant
runs only to the project; everyone else receives the contribution under FSL-1.1-MIT like the rest
of the source.

## Reporting security issues

Please don't file a public issue for a suspected vulnerability. Report it privately through GitHub's
security advisory form for this repository instead.
