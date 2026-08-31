# Per-User Host Locks

Native compiler work and host package-manager mutations are serialized by per-user lock families.
This prevents concurrent worktrees on a persistent runner from competing for Xcode, the .NET
workload manager, or their shared host state. The client wrappers delegate to the pinned
`vouchington with-host-lock` command; do not add another lock around a wrapper that already owns one.

## Native build lock

Use the client wrapper for compiler-heavy commands:

```sh
bash swift-clients/tooling/with-build-lock.sh swift test --package-path swift-clients/core --force-resolved-versions
bash dotnet-clients/tooling/with-build-lock.sh dotnet build dotnet-clients/Voucha.DotNet.sln --no-restore
```

Both wrappers acquire the `expensive-build` family. They wait for 60 seconds by default; set
`VOUCHA_BUILD_LOCK_WAIT_SECONDS` to a positive integer no greater than 300 only when a known
healthy command needs a different admission window. Locally, failure to acquire the lock is
fail-closed and the command has no wrapper timeout. In GitHub Actions, the default is to continue
unlocked after an acquisition timeout, with a 300-second command cap. The supported overrides are:

- `VOUCHA_BUILD_LOCK_ON_ACQUIRE_TIMEOUT=fail|run-unlocked`
- `VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS=<nonnegative integer>`

Keep one logical compiler command under one lock owner.

## Host package-manager lock

Use `host-package-manager` for mutations of machine-level tooling, such as installing the pinned
MAUI workload in CI. It is separate from `expensive-build`, waits up to 300 seconds, and must fail
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
