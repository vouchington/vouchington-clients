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
