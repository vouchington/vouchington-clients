# Private CI runner policy

This repository is private and does not use GitHub-hosted runners. Every workflow job must select
an organization-owned runner with an explicit self-hosted label set:

- `[self-hosted, Linux]` is the default for credential-free Linux checks, workflow routers, and
  API-only jobs.
- `[self-hosted, macOS, Tests]` is used for macOS native build, test, and dead-code checks.
- `[self-hosted, Linux, Docker, Tests]` is used for Linux checks that run pinned tool containers.

Which native tests belong on each label, and when a second OS is forbidden, is documented in
[native CI test placement](native-ci-test-placement.md).

Do not add `ubuntu-*`, `macos-*`, or `windows-*` labels to a workflow or matrix. Public companion
repositories may use GitHub-hosted capacity under their own policy; that exception does not apply
here.

## Persistent-runner cleanup

The self-hosted fleet is persistent and is not an isolation boundary. Any job that checks out or
executes pull-request code must:

1. Check out the pull request's exact trusted base SHA first with `clean: false` and
   `persist-credentials: false`; this establishes the Git worktree without executing candidate
   repository configuration or retaining checkout credentials. On a default-branch push, use the
   event SHA for both trusted and candidate checkout.
2. Immediately run `pnpm dlx vouchington-tooling@0.1.5 clean-workspace` with
   `PRESERVE_NODE_MODULES=false`, but bootstrap that command from `$RUNNER_TEMP`, not the
   workspace. Pin `NPM_CONFIG_REGISTRY` to the public registry and point the global and user npm
   configuration at `/dev/null`; pass the workspace to the already-installed tool only when it
   starts cleanup. Cleanup owns removal of stale files while preserving only explicitly requested
   dependencies. It must not be expected to initialize an empty workspace.
3. Check out the exact candidate SHA and run the job's candidate work.
4. In an `if: always()` final path, restore the trusted base SHA before running the same pinned
   isolated cleanup with `PRESERVE_NODE_MODULES=false`. Do not let a candidate `.npmrc` choose the
   registry or other npm configuration used to bootstrap cleanup.

API-only jobs do not need checkout cleanup, but still use the appropriate self-hosted runner. The
Native contract producers follow the same cleanup boundary because they handle trusted checkouts,
generated artifacts, or provider tooling on persistent hosts.

## Shared host tool isolation

Multiple runner processes on one persistent host share `$HOME`. Jobs that install or execute host
tooling must point that tooling at `$RUNNER_TEMP` so concurrent jobs do not write and exec the same
binary. That race surfaces as Linux `ETXTBSY` when one job extracts or replaces a file another job
is spawning.

- .NET uses `DOTNET_INSTALL_DIR=$RUNNER_TEMP/voucha-dotnet-sdk`.
- mise uses `MISE_DATA_DIR=$RUNNER_TEMP/mise` and `mise_dir: ${{ runner.temp }}/mise` on
  `jdx/mise-action`. Do not let mise-action default to `~/.local/share/mise`.

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
Dependabot head.

Read-only publishers revalidate the live Dependabot identity, base and head SHAs, raw Git change
types, candidate and artifact allowlists, artifact provenance, hashes, and the head lease. They
construct repair commits with temporary Git indexes; only the final `git push --force-with-lease`
receives `DEPENDABOT_AUTOMERGE_TOKEN`. That secret must stay scoped to the repository Contents and
Pull requests access needed by Dependabot auto-merge and these single repair pushes. See
[Frozen-install policy](reference-dependency-updates-frozen-install-policy.md) for the authoritative
manifests and generated-output sets.
