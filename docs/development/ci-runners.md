# GitHub-hosted CI runner policy

This repository uses GitHub-hosted ephemeral runners. Every workflow job must select a label from
the closed allowlist:

- `ubuntu-slim` is the default for credential-free Linux checks, workflow routers, aggregators, and
  API-only jobs.
- `ubuntu-latest` is used for Linux work that needs Docker, a tool container, or a full GitHub
  Ubuntu image.
- `macos-latest` is used for macOS native build, test, and dead-code checks.

Which native tests belong on each label, and when a second OS is forbidden, is documented in
[native CI test placement](native-ci-test-placement.md).

Do not add `self-hosted`, `windows-*`, or unlisted `ubuntu-*` / `macos-*` labels. Windows remains
out of scope until a hosted Windows job is actually needed.

Hosted VMs are single-job and discarded after the run. Jobs check out the revision they execute and
must not run persistent-workspace cleanup, restore a trusted base only to wipe the disk, or isolate
`$HOME` against a shared persistent host.

Jobs may restore hash-keyed GitHub Actions caches for package stores and checksum-verified download
archives. Frozen install commands remain the authority (`--frozen-lockfile`, `--locked-mode`,
`--force-resolved-versions`). Extracted toolchains, SDKs, and NDK trees stay in `$RUNNER_TEMP` and
are not cached.

## pnpm

pnpm is not pinned. No `package.json` declares `packageManager`, `devEngines.packageManager`, or
`engines.pnpm`. Under such a pin, pnpm 12 writes `pnpm-lock.yaml` as two YAML documents, and
single-document readers such as Dependabot
([dependabot/dependabot-core#15904](https://github.com/dependabot/dependabot-core/issues/15904))
misread it. Unpinned, the lockfile stays one document.

- Locally, install any pnpm 12 release and run `pnpm` from `PATH`.
- In CI, every job or composite action that runs pnpm adds `pnpm/action-setup` directly after
  `actions/setup-node`. Its only input is `version: 12`, a bare major. The action installs the pnpm
  12 release it bundles, so CI changes pnpm only through a reviewed `pnpm/action-setup` bump. The
  `/plan` and `/fix` gate jobs stay checkout-free: the action needs no `package.json`, and those
  jobs only run `pnpm dlx`.
- Workflows, scripts, and the native lock wrappers run `pnpm` from `PATH`. Nothing bootstraps pnpm
  through Corepack or a versioned `npx pnpm@<version>` call.
- Dependabot runs the pnpm bundled with its updater image.

[`tests/pnpm-setup.test.mjs`](../../tests/pnpm-setup.test.mjs) enforces these rules, including one
action release and one pnpm major across every call site. To change the major, update every
`pnpm/action-setup` call in the same change.

## Event-driven orchestration

Workflow dependencies must use GitHub events, job dependencies, or exact completion reports. Do
not keep a runner alive to poll pull requests, checks, or other workflow runs. Bounded retries for
transient API failures and short failure-diagnostic collection windows are not orchestration
polling and remain permitted.

## Dependabot native repair

`repair-dependabot-dotnet-locks.yml` regenerates the .NET restore matrix's seven lockfiles, while
`dependabot-automerge.yml` repairs Android Skip's checked archive version and checksum before
enabling auto-merge. Their `pull_request_target` producers check out only the exact default-branch
base and fetch candidate dependency files as inert API data. They never check out or execute the
Dependabot head. Fork pull requests run on hosted `pull_request` without repository secrets; do not
reintroduce self-hosted labels for untrusted code.

Read-only publishers revalidate the live Dependabot identity, base and head SHAs, raw Git change
types, candidate and artifact allowlists, artifact provenance, hashes, and the head lease. They
construct repair commits with temporary Git indexes; only the final `git push --force-with-lease`
receives `DEPENDABOT_AUTOMERGE_TOKEN`. That secret must stay scoped to the repository Contents and
Pull requests access needed by Dependabot auto-merge and these single repair pushes. See
[Frozen-install policy](reference-dependency-updates-frozen-install-policy.md) for the authoritative
manifests and generated-output sets.
