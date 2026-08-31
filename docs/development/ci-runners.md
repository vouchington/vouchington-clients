# Private CI runner policy

This repository is private and does not use GitHub-hosted runners. Every workflow job must select
an organization-owned runner with an explicit self-hosted label set:

- `[self-hosted, Linux]` is the default for credential-free Linux checks, workflow routers, and
  API-only jobs.
- `[self-hosted, macOS, Tests]` is used for macOS native build, test, and dead-code checks.
- `[self-hosted, Linux, Docker, Tests]` is used for Linux checks that run pinned tool containers.

Do not add `ubuntu-*`, `macos-*`, or `windows-*` labels to a workflow or matrix. Public companion
repositories may use GitHub-hosted capacity under their own policy; that exception does not apply
here.

## Persistent-runner cleanup

The self-hosted fleet is persistent and is not an isolation boundary. Any job that checks out or
executes repository code must:

1. Run `pnpm dlx vouchington-tooling@0.1.5 clean-workspace` before its first checkout with
   `PRESERVE_NODE_MODULES=false`.
2. Set `clean: false` and `persist-credentials: false` on checkout steps. Workspace cleanup owns
   removal of stale files while preserving only explicitly requested dependencies.
3. Run the same pinned cleanup in an `if: always()` final step, again with
   `PRESERVE_NODE_MODULES=false`.

API-only jobs do not need checkout cleanup, but still use the appropriate self-hosted runner. The
Native contract producers follow the same cleanup boundary because they handle trusted checkouts,
generated artifacts, or provider tooling on persistent hosts.

## Event-driven orchestration

Workflow dependencies must use GitHub events, job dependencies, or exact completion reports. Do
not keep a runner alive to poll pull requests, checks, or other workflow runs. Bounded retries for
transient API failures and short failure-diagnostic collection windows are not orchestration
polling and remain permitted.

## Dependabot native repair

`repair-dependabot-native.yml` repairs generated native dependency outputs that Dependabot cannot
derive: the .NET restore matrix's seven lockfiles and Android Skip's checked archive checksum. Its
`pull_request_target` producer checks out only the exact default-branch base on macOS and fetches
candidate dependency files as inert API data. It never checks out or executes the Dependabot head.

The Linux publisher has read-only workflow permissions. It revalidates the live Dependabot identity,
base and head SHAs, candidate and artifact allowlists, artifact provenance, and the head lease; only
its final `git push --force-with-lease` receives `DEPENDABOT_AUTOMERGE_TOKEN`. That secret must stay
scoped to the repository Contents and Pull requests access needed by Dependabot auto-merge and this
single repair push.
