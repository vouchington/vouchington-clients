# Host Locks

Voucha CI runs on GitHub-hosted, ephemeral runners. Repository workflows therefore do not use
host-lock wrappers or shared-host `$HOME` isolation: every job receives an isolated VM and
coordinates only the processes it starts in that job.

Local concurrent worktrees on one machine can still serialize compiler-heavy commands and host
package-manager mutations through the client wrappers. Those wrappers are optional locally and must
not be reintroduced in GitHub Actions.

## Native build lock

Use the client wrapper only for local compiler-heavy commands:

```sh
bash swift-clients/tooling/with-build-lock.sh swift test --package-path swift-clients/core --force-resolved-versions
bash dotnet-clients/tooling/with-build-lock.sh dotnet build dotnet-clients/Voucha.DotNet.sln --no-restore
```

Both wrappers acquire the `expensive-build` family. They wait for 60 seconds by default; set
`VOUCHA_BUILD_LOCK_WAIT_SECONDS` to a positive integer no greater than 300 only when a known
healthy command needs a different admission window. Locally, failure to acquire the lock is
fail-closed and the command has no wrapper timeout.

Keep one logical compiler command under one lock owner.

The wrappers and the .NET harness `restore` check run `pnpm exec vouchington with-host-lock` with
the `pnpm` on `PATH`. Install pnpm 12 and run `pnpm install` before using them. See
[pnpm](ci-runners.md#pnpm).

## Host package-manager lock

Use `host-package-manager` locally for mutations of machine-level tooling, such as installing the
pinned MAUI workload. It is separate from `expensive-build`, waits up to 300 seconds, and must fail
closed. Ordinary restore, build, and test commands must not install SDKs or workloads as a side
effect; install the exact versions declared in [`global.json`](../../global.json) first.

## Native harness timeout classification

The Swift and .NET harnesses preserve command output and classify only two proven lock outcomes:

- `host-timeout`: exit status 124 plus the matching `with-host-lock: ... command exceeded ...`
  marker for `expensive-build` or `host-package-manager`.
- `lock-timeout`: exit status 1 plus the matching `with-host-lock: ... lock not acquired ...`
  marker for the same lock families.

Every other failure, including host-pressure diagnostics without the exact wrapper marker, is a
`check-failure`. Treat that as a real failure until a reproduction or a concrete infrastructure
signature proves otherwise; these classifications do not authorize a blind rerun.
