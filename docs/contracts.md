# Vouchington contract boundary

Native clients do not read arbitrary files from Vouchington. `contracts/vouchington.json` fixes the
repository, one pinned full 40-character `commit`, and the allowed source paths. There is no
branch or ref to track: every checkout, local or CI, resolves the same immutable producer
revision.

## Pulling the contract

`pnpm run contracts:pull` resolves the contract root and makes the generated native localization
available in this checkout:

1. With no `VOUCHA_CONTRACT_ROOT`, it shallow-fetches exactly the pinned commit into
   `.cache/vouchington/<sha>` (gitignored, SHA-keyed, reused when present, set
   `VOUCHA_CONTRACT_CACHE_DIR` to share it across worktrees), asserts `HEAD` equals the pin, and
   runs `pnpm install --frozen-lockfile --ignore-scripts` there for the exporter.
2. With `VOUCHA_CONTRACT_ROOT` set, it uses that absolute path as-is and fetches nothing. Use this
   to develop against an unmerged producer pull request (a full checkout of that branch) or, in
   CI, a verified staged contract root.
3. A root that contains the producer exporter (`dev/native-localization.mts`) is a full checkout:
   the exporter runs against this client checkout into an isolated temporary stage. Any other root
   must already be a staged contract root. Either way the declared paths must exist as real
   directories without symlinks.
4. The Swift and .NET `Generated` localization trees are replaced (the previous tree is kept as a
   sibling backup during replacement and recovered if interrupted) and the resolved root is
   printed on stdout, with progress on stderr:

   ```sh
   export VOUCHA_CONTRACT_ROOT="$(pnpm run --silent contracts:pull)"
   ```

Generated localization is never committed. SwiftPM (`swift-clients/ui/Package.swift`),
`swift-clients/tooling/generate.sh`, the Swift and .NET harnesses, and the
`Voucha.Client.Core` MSBuild project fail with a `contracts:pull` hint when it is missing. Swift
and .NET test targets read fixtures and request contracts from `VOUCHA_CONTRACT_ROOT` through the
repository-local `swift-clients/test-support` package and `ContractPaths`; they never discover a
checkout.

`pnpm run contracts:check` is the non-mutating, fail-closed parity gate: it resolves and stages the
same way as `contracts:pull` (rerunning the exporter against this checkout, so it validates the
current product localization references) and fails if the generated trees are missing or differ
byte-for-byte. It never writes this repository.

## Bumping the pin

A change that needs a new producer contract lands in Vouchington first. Then the paired client
change is a one-line pin bump:

```sh
gh api repos/vouchington/vouchington/commits/main --jq .sha   # edit "commit" in contracts/vouchington.json
pnpm run contracts:pull && pnpm run contracts:check
```

The pin must be reachable from producer `main` before the client change merges. The default
workflow enforces it (see below), so a client pull request pinned to an unmerged producer commit
stays red and dependency-blocked. To exercise a client change against an open producer pull
request, point `VOUCHA_CONTRACT_ROOT` at a checkout of that branch locally, or dispatch the
[paired native contract review evidence](development/paired-native-contract-attestation.md)
workflow, which stages the producer pull request's merge commit instead of the pin.

## CI

The native test workflow runs for pull requests and every push to `main`. Its producer job reads the
candidate's pin as data and asks GitHub whether the commit is reachable from producer `main`
(`compare/<sha>...main` must be `identical` or `ahead`) before it checks out or installs anything
from Vouchington, so privileged jobs only execute merged producer code. It then checks out exactly
that commit, stages the contract with the producer exporter against the candidate client checkout,
and copies only the allowlisted trees into a one-day, run-scoped GitHub Actions artifact. Its
manifest binds every file's path, byte length, and SHA-256 digest to the pinned Vouchington revision,
candidate event and revisions, repository, and workflow run identity. Pull-request manifests also
bind the exact pull-request number and merge revision; main manifests bind the push's before and
after revisions. Secretless downstream jobs check out that same workflow revision so local
composite actions match, download that exact run's artifact, verify the complete manifest before
reading it, revalidate the event identity, assert the artifact's Vouchington SHA equals the
candidate's pin, and only then execute the candidate checkout against the verified inputs. Each
candidate job runs `contracts:pull` with the verified artifact as `VOUCHA_CONTRACT_ROOT`, which
writes the generated localization before any build. The validate and release workflows run the same
pull against the pin through the `pull-vouchington-contract` action. All detailed native jobs
converge on the required `Tests` gate. Superseded pull-request runs are cancelled, while main runs
are never cancelled; no runner polls another workflow's state.

This short-lived handoff supersedes issue #1's original prohibition on hosted contract artifacts
and digest metadata by explicit maintainer approval. It does not publish a source bundle or retain
a durable contract package.
