# Vouchington contract boundary

Native clients do not read arbitrary files from Vouchington. `contracts/filaments.json` fixes the
repository, branch, and allowed source paths. CI owns obtaining a normal full Vouchington checkout,
then supplies its root explicitly through `VOUCHA_FILAMENTS_CONTRACT_ROOT`; the contract command
validates the required layout and never performs repository acquisition itself.

The localization contract is the generated Swift and .NET resource trees. When the trusted
Vouchington checkout supplies its native-localization exporter, staging runs that exporter in
preference to legacy generated trees so an extraction can publish the same current contract before
those trees disappear. Older producer revisions without the exporter fall back to their generated
trees. The contract command verifies the explicit root and every allowlisted source tree is a real
directory with no symlinks. It stages each generated localization copy, preserves the previous
directory as a sibling backup during replacement, and automatically recovers that backup if a sync
is interrupted before publication. Run the sync again after an interruption.

Swift test targets share the same root, required-path, and fixture-path validation through the
repository-local `swift-clients/test-support` package.

`contracts:check` is fail-closed: a missing root or source tree, a symlink, or output mismatch
fails the command. In CI this is the byte-for-byte assertion that the clients remain synchronized
with Vouchington. The privileged producer executes only assertion and artifact code from the trusted
base revision; the pull request checkout is treated as data and is never executed by producer
tooling. The candidate destination must be an absolute checkout path, and every path
component is verified as a real directory rather than a symlink. It never fetches or writes. Only
`contracts:sync` replaces generated outputs in this repository.

The native test workflow runs for pull requests and every push to `main`. It copies only the three
allowlisted contract trees into a one-day, run-scoped GitHub Actions artifact. Its manifest binds
every file's path, byte length, and SHA-256 digest to the immutable Vouchington revision, candidate
event and revisions, repository, and workflow run identity. Pull-request manifests also bind the
exact pull-request number and merge revision; main manifests bind the push's before and after
revisions. The privileged producer job uses trusted base-branch or main-push tooling. Secretless
downstream jobs check out the same workflow revision GitHub already loaded so local composite
actions match the running workflow, download that exact run's artifact, verify the complete
manifest before reading it, revalidate the event identity, and only then execute the candidate
checkout against the verified inputs. All detailed native jobs converge
on the required `Tests` gate. Superseded pull-request runs are cancelled, while main runs are never
cancelled; no runner polls another workflow's state.

This short-lived handoff supersedes issue #1's original prohibition on hosted contract artifacts
and digest metadata by explicit maintainer approval. It does not publish a source bundle, retain a
durable contract package, or use sparse checkout.
