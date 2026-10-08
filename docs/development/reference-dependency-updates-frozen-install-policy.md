# Frozen-install policy

Dependency changes are complete only when their authoritative manifest, every derived lock, and the
pinned toolchain agree. Review the manifest and lock diff together, then run the corresponding
frozen verification before merging.

## pnpm

`pnpm-lock.yaml` is canonical for the Node tooling. Verify it with
`pnpm install --frozen-lockfile` under any pnpm 12 release; the lockfile must not change. pnpm
itself is deliberately unpinned. See [pnpm](ci-runners.md#pnpm).

## SwiftPM

Each package's `Package.resolved` is canonical for `swift-clients/core`, `swift-clients/ui`, and
`swift-clients/apps/android`. Normal build, test, and dead-code commands use
`--force-resolved-versions`. To update a package intentionally, run:

```sh
swift package --package-path swift-clients/<core|ui|apps/android> update
```

Review and commit the resulting resolved file, then rerun the strict client checks. An Android Skip
update must also change the matching version and archive checksum in
[`materialize-skip-sdk.sh`](../../swift-clients/apps/android/tooling/materialize-skip-sdk.sh); the
resolved pins and materializer are one update unit.

## NuGet

`dotnet-clients/Directory.Packages.props` is the authoritative central package-version manifest.
The root [`global.json`](../../global.json) is authoritative for the SDK and workload set. The
repository commits seven NuGet locks, covering the portable solution, rendered app tests, and both
Mac Catalyst runtime identifiers.

For a deliberate NuGet update, use the matrix helper with isolated repository-local NuGet state:

```sh
bash dotnet-clients/tooling/restore-locks.sh update
bash dotnet-clients/tooling/restore-locks.sh verify
```

The helper requires the exact SDK and workload from `global.json`, resolves only through the
repository NuGet configuration, and verifies all seven regular lock files. Do not regenerate a
subset of locks or use an ambient SDK, package cache, or workload installation as evidence.

Dependabot may propose literal central NuGet version updates. Its trusted repair path regenerates
the seven locks from the validated manifest delta and publishes only those locks with provenance
and hashes. NuGet updates still require manual merge; SDK, workload, and `MauiVersion` changes are
manual-only.

## Audit evidence

Record the current and candidate versions, source release metadata, the authoritative manifest
paths, every expected lock or materializer path, and the exact frozen commands that passed. A
dependency update must not use version overrides to hide a stale transitive graph. Cached package
state on a runner is operational reuse, not authoritative resolution.

Local concurrent worktrees may still use [host locks](host-locks.md) for compiler-heavy checks and
host package-manager mutations. GitHub Actions must not.
