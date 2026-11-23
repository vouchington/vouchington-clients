# GitHub-hosted CI runner policy

This repository uses GitHub-hosted ephemeral runners. Every workflow job must select a label from
the closed allowlist:

- `ubuntu-slim` is the default for credential-free Linux checks, workflow routers, aggregators, and
  API-only jobs.
- `ubuntu-latest` is used for Linux work that needs Docker, a tool container, or a full GitHub
  Ubuntu image.
- `macos-latest` is used for macOS native build, test, and dead-code checks.
- `windows-latest` runs changed-file-routed .NET `BitmapDecoder` tests, the pinned Windows App SDK
  language-model surface probe, and the actual Windows MAUI app compile check. These exercise
  Windows-only source and WinUI. The surface probe reads restored package metadata and compiles the
  projected admission APIs. It does not generate with the language model.

Which native tests belong on each label, and when a second OS is forbidden, is documented in
[native CI test placement](native-ci-test-placement.md).

Public repositories automatically receive the public allocation for these labels: Linux
`ubuntu-latest` has four vCPUs and 16 GB RAM, while `ubuntu-slim` remains single-CPU. Keep compiler
parallelism adaptive to the host. Linux ARM migration requires measured benefit and native-tool
compatibility; the current Android NDK compiler is x86_64-only. `macos-latest` already uses ARM.

The MAUI Mac Catalyst smoke build is required. Its Xcode selector resolves the macOS SDK with
`xcrun`, checks the SDK's Mac Catalyst support and asset compiler, and fails if the pinned workload's
required Xcode version is unavailable. A missing toolchain must not turn an unrun app build green. The Mac Catalyst GUI preflight is a
separate required job with a 10-minute limit. It does not add steps to the MAUI smoke job.

Do not add `self-hosted`, other `windows-*`, or unlisted `ubuntu-*` / `macos-*` labels. The three
Windows jobs run only for their relevant source/toolchain changes; Windows app packaging and signing remain out of scope.

Hosted VMs are single-job and discarded after the run. Jobs check out the revision they execute and
must not run persistent-workspace cleanup, restore a trusted base only to wipe the disk, or isolate
`$HOME` against a shared persistent host.

Jobs may restore hash-keyed GitHub Actions caches for package stores and checksum-verified download
archives. Frozen install commands remain the authority (`--frozen-lockfile`, `--locked-mode`,
`--force-resolved-versions`). Extracted toolchains, SDKs, and NDK trees stay in `$RUNNER_TEMP` and
are not cached. Shared pnpm stores restore on every run. A missing primary key is saved only after
the frozen install on a `push` to `refs/heads/main`. Pull requests and merge-queue runs restore and
install, and they do not save. Another job may reserve that key first; the lost save does not fail the run.

The Mac Catalyst app job (`dotnet-maui`) and the Windows MAUI app job (`dotnet-windows-maui`) do
not run on a `main` push, so nothing saves the `nuget-maui-*` or `nuget-windows-maui-*` package
caches. Pull requests and merge groups still restore them, and still run the locked restores and
the app builds on a miss. The rendered-page job and the Mac Catalyst GUI preflight restore the same
`nuget-maui-*` key, so they also start cold once the existing entry is evicted. That costs one NuGet
download per run; it is accepted rather than rebuilding the app on `main` only to save a cache.

The Windows image decoder NuGet cache keeps the main-only save policy, after the job's tests succeed.
Its exact key, package paths, and workload-specific restore prefix stay the same. A PR with new
dependencies may restore a main prefix and download the missing packages, but it does not save a
branch-local cache copy. This reduces competing cache writes; it does not guarantee that a cache
remains available or that a job gets faster.

## Merge queue and main pushes

The required checks `validate` (`validate.yml`) and `Tests` (`native-contract-tests.yml`) run on
`pull_request` and on `merge_group` (`checks_requested`). The default-branch ruleset requires the
merge queue (squash), so the required checks run against the exact combined result of the queued
pull requests on top of `main`. Two pull requests that each pass alone can still conflict
semantically, as a duplicate Swift declaration once did; the queue catches that before it lands
(#705). A `merge_group` run checks out `github.sha` (the queue commit, equal to
`merge_group.head_sha`), uses `merge_group.base_sha` where a pull request uses its base SHA, and has
no `pull_request` or `before` payload.

A push to `main` no longer runs tests. It is a build-and-warm path: it keeps the
remaining cache saves, and it skips the test jobs, the MAUI app builds,
the test steps inside warm jobs, and the `Tests` aggregate. Caches saved by a `merge_group` run are
scoped to its `gh-readonly-queue/...` ref and cannot be restored by pull requests or `main`, while
`merge_group` runs can restore `main` caches. So cache saves stay on the `main` push; an ejected
queue group must not create a cache entry.

A warm job is a no-op when its cache already exists. Three jobs probe first, on a main push only,
with `actions/cache/restore` and `lookup-only: true`, and guard every later step with
`steps.warm.outputs.cache-hit != 'true'` (pull requests and merge groups skip the probe, so nothing
changes for them):

- `test-swift-android-libs` probes the checksummed-archive key (a hash of `materialize-skip-sdk.sh`,
  the same key `prepare-android-archives` restores) and `dotnet-catalyst-gui` probes the static
  Appium Mac2 key. Both keys already depend only on real inputs, so a hit skips the whole job after
  the checkout.
- `build-swift-android` saves its Gradle task-output cache under a per-revision chain key, so that
  key can never hit on a new merge. The probe instead looks up a small marker cache keyed on the
  inputs the app build reads: the Android app, the core and UI sources (not their tests), the shared
  tooling, `contracts/vouchington.json`, `.mise.toml`, the Android and mise composite actions, and
  this workflow file. A hit means the same source set already built on `main` and saved its caches,
  so the build is skipped and no cache is saved under a new key. A miss builds as before, then saves
  the marker last, so a failed build never records one. Merges that only touch tooling
  dependencies, such as the `no-mistakes` bumps, hit.
- `build-macos-app` and `build-ios-tests` pass `warm-only` to `resolve-swift-packages`, which only
  looks the clones cache up and fetches packages on a miss. Their build steps already skip on a push.

`dotnet-maui` and `dotnet-windows-maui` skip a `main` push. They only compiled unsigned CI binaries
that nothing ships, saved a NuGet cache, and uploaded `voucha-dotnet@<sha>` debug files that match
only those binaries (measured on main push run 45042094835: 437 s on macOS and 284 s on Windows).
The Mac Catalyst Release build runs on merge groups only; a pull request compiles the Debug app in `dotnet-catalyst-gui` instead. The Windows build keeps pull requests and merge groups. See
[native Sentry releases](native-sentry-releases.md#trusted-uploads) for the .NET upload.

Add a new required test to a `merge_group`-capable job without a `github.event_name != 'push'`
condition only if it also warms a cache or uploads symbols; do not build only to warm a cache. `tests/merge-queue-workflow.test.mjs`
pins the job list that remains on a `main` push.

## mise binary cache

`jdx/mise-action` runs only in `.github/actions/setup-mise` and in the `setup-mise-toolchain` and `setup-swift-native` composites, which carry the same steps inline (a composite's `./` path resolves against `GITHUB_WORKSPACE`, so it cannot call a sibling from the `trusted-clients` checkout; a test pins the three copies together). A job that downloaded the
mise binary from a github.com release asset failed on a 403 or a curl exit 56 and ejected an unrelated
pull request from the merge queue (issue #766), and with `cache: false` mise-action never caches the
binary. The action pins one mise release (its `version` input, so the cache key is stable), restores
`bin/mise`, `bin/mise-version` and the signed `SHASUMS256-<version>.txt` files with `actions/cache`, and
only then runs mise-action. mise-action finds the binary, checks it against the cached signed checksums
without the network, and skips the download. Only a `push` to `main` saves the cache, keyed on the mise
version, `runner.os` and `runner.arch`; pull requests and merge groups restore only. Tool installs stay
uncached (`cache: false`). To bump mise, change the version in all three. The first `main` push per
runner OS and architecture refills the cache, and the `build-swift-android` warm marker hashes the
action, so that push rebuilds instead of skipping. Until then a key miss downloads as before. There are
no retries.

## pnpm

pnpm is not pinned. No `package.json` declares `packageManager`, `devEngines.packageManager`, or
`engines.pnpm`. Under such a pin, pnpm 12 writes `pnpm-lock.yaml` as two YAML documents, and
single-document readers such as Dependabot
([dependabot/dependabot-core#15904](https://github.com/dependabot/dependabot-core/issues/15904))
misread it. Unpinned, the lockfile stays one document.

- Locally, install any pnpm 12 release and run `pnpm` from `PATH`.
- In CI, every job or composite action that runs pnpm adds `pnpm/action-setup` directly after
  `actions/setup-node`. Its only input is `version: latest-12`, so the action runs
  `pnpm self-update latest-12` and CI uses the newest mature pnpm 12 release. `self-update` ignores
  `pnpm-workspace.yaml`, so each setup step also sets `PNPM_CONFIG_MINIMUM_RELEASE_AGE` to the
  workspace `minimumReleaseAge` (7 days); without it pnpm's one-day default applies. When the
  newest 12.x release is younger than that, `self-update` installs the newest mature one instead.
  New 12.x releases reach CI without a pull request; moving to pnpm 13 needs one.
- Do not pass a bare `version: 12`. That range matches the pnpm the action bundles (12.3.4 in
  v6.1.0), so the action skips `self-update` and keeps it. pnpm 12.3.0 through 12.4.2 make
  `pnpm dlx` exit 1 with `ERR_PNPM_IGNORED_BUILDS` for packages with build scripts, such as
  `wrangler`; 12.5.0 fixed it.
- Workflows, scripts, and the native lock wrappers run `pnpm` from `PATH`. Nothing bootstraps pnpm
  through Corepack or a versioned `npx pnpm@<version>` call.
- Dependabot runs the pnpm bundled with its updater image.

[`tests/pnpm-setup.test.mjs`](../../tests/pnpm-setup.test.mjs) enforces these rules, including one
action release and one `latest-<major>` across every call site. To change the major, update every
`pnpm/action-setup` call in the same change.

### Release age

Automated updates wait 7 days before adoption: pnpm installs and every Dependabot ecosystem. First-party packages and dated security-fix exemptions skip the wait. Exact pins (mise tools, the .NET SDK and workload, Swift and Xcode) change only in a reviewed pull request and have no age gate of their own.

| Source                                         | Gate                                                                                                                                                              |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| pnpm installs                                  | `minimumReleaseAge: 10080` in `pnpm-workspace.yaml`; first-party packages, and exact security-fix versions with a removal date, are in `minimumReleaseAgeExclude` |
| CI `pnpm self-update`                          | `PNPM_CONFIG_MINIMUM_RELEASE_AGE: '10080'` on every `pnpm/action-setup` step                                                                                      |
| Dependabot npm, actions, NuGet, SwiftPM        | `cooldown.default-days: 7` on every entry; npm excludes its first-party packages. The other ecosystems have none to exclude                                       |
| NuGet, SwiftPM, GitHub Actions `uses:` pins    | No install-time age gate exists; the pinned manifest plus Dependabot cooldown is the gate                                                                         |
| mise tools, .NET SDK and workload, Xcode, curl | Pinned to exact versions; they change only through a reviewed pull request                                                                                        |

[`tests/release-age-policy.test.mjs`](../../tests/release-age-policy.test.mjs) and
[`tests/pnpm-setup.test.mjs`](../../tests/pnpm-setup.test.mjs) enforce the pnpm and
Dependabot values. Dependabot security updates are not delayed by the cooldown.

## Android validation

Swift Android native compilation and APK assembly run in one macOS job that executes
`pre-push.sh --phase app`: a single Gradle invocation builds the native Swift product, the Kotlin
libraries and both APKs, with Gradle parallelism enabled. Host Swift package tests and native
test-library preparation run in parallel jobs. The job still verifies the exact native contract
but omits candidate pnpm installation; its candidate tooling uses Node builtins, shell, Swift, and
Gradle. Extracted Swift, Android SDK, and NDK toolchains are materialized in the job. The existing
cookie runtime job also reports the stable `Swift Android Skip` check: it first requires the app
job, host tests, and test libraries to succeed, then runs the full cookie restart proof. `Tests`
requires that job's result directly. The job budget is 35 minutes, a safety bound and not evidence
that the under-ten-minute target has been met.
The dedicated host job runs the planner-selected source tests with
`run-android-host-swift-tests.sh --package-path swift-clients/apps/android --filter
VouchaAndroidTests` on Xcode 26.6 with Swift 6.3.3. Swift 6.4 remains on the Android Gradle
path, and the later emulator instrumentation uses the exact candidate APKs. The Gradle build
records a pinned `--profile` summary.
Reported task and category durations may overlap and are not a critical-path sum.

The Android archive and Gradle caches restore by pinned input keys on every run. Gradle's
task-output build cache is separate from its user home: local builds use the canonical Darwin
temporary directory, while GitHub Actions uses a private stable runner-home path because cache
archives retain absolute paths across hosts. The app job restores the task-output cache. Test-library validation uses SwiftPM without Gradle, so it does not download
that cache. The app job uses the same checked task-output path that `pre-push.sh` gives Gradle. Only a `main` push writes a missing key:
the test-library phase saves the shared checksummed-download archive after SDK materialization,
and the app job saves its Gradle user home and task-output snapshot after APK upload. The
task-output save accepts at most 512 MiB of logical regular-file bytes, 100,000 members, and a
30-second scan; an oversized cache simply stays job-scoped. Full app tasks and candidate APK
checks remain required on hits.
Pull requests still compile from source on a miss, but do not write large per-PR cache copies
that compete with the default-branch entries. The archive key follows the checksum-bearing
materialization script, so an unrelated workflow edit does not invalidate verified downloads.
The task-output key partitions runner OS, architecture, canonical cache path, pinned Swift/Skip/
Gradle configuration, candidate revision, and snapshot kind (`app`). A pull request first requests
its exact base revision; a `main` push requests its own revision (for reruns), then its exact
predecessor. The job falls back to a compatible prefix within the same OS, architecture, path, and
configuration partition. Gradle still fingerprints task inputs and executes missing work. Pull
requests never publish persistent snapshots.

A missing compatible cache or changed configuration still requires a cold build. The single app
job has a 35-minute budget; native performance and warm-cache timing still require hosted-run
validation.

The cookie restart runner bounds emulator shutdown after the instrumentation phases. It stops
and reaps only its captured emulator child, including a child that ignores termination or is
stopped. Cleanup preserves an earlier test failure and fails an otherwise successful run if the
owned emulator cannot be stopped. Host regression tests cover shutdown behavior; they do not
replace emulator instrumentation.

The app build uses pinned Gradle's local `--profile` report. CI writes its configuration,
dependency, and task durations, plus bounded generated-state size measurements, to the existing
job summary and log. Task durations can overlap and do not represent critical-path wall time.
The report is diagnostic only: the same app and instrumentation tasks remain required, and a
profiling-summary error cannot turn a failed build green. Generated build trees and extracted
toolchains remain job-scoped; the summary does not publish or cache them.

## Event-driven orchestration

Workflow dependencies must use GitHub events, job dependencies, or exact completion reports. Do
not keep a runner alive to poll pull requests, checks, or other workflow runs. Bounded retries for
transient API failures and short failure-diagnostic collection windows are not orchestration
polling and remain permitted.

## Dependabot native repair

`repair-dependabot-dotnet-locks.yml` regenerates the .NET restore matrix's twelve lockfiles, while
`dependabot-swift-android-repair.yml` repairs Android Skip's checked archive version and checksum.
No workflow arms auto-merge; Dependabot PRs merge through the ordinary triage path. At cutover,
auto-merge was disabled on the one open Dependabot PR that the removed workflow had armed (#696),
so no earlier arming bypasses triage. Both
workflows run on `pull_request` (the org dropped `pull_request_target`). Their jobs run only when
`github.actor` and the PR author are `dependabot[bot]` and the head branch lives in this
repository; fork pull requests never reach a job, let alone a write path. Jobs still check out
only the exact live default-branch base SHA (`persist-credentials: false`) and fetch candidate
dependency files as inert API data, so repair scripts and `pnpm install` never execute the
Dependabot head tree.

Trust model under `pull_request`: the workflow definition itself comes from the pull request's
merge ref, not from the default branch, so the base-SHA checkout protects the scripts but not the
workflow YAML or its `uses:` refs. To close that, repair never runs when a PR changes anything
under `.github/` (including a rename away from it). An early job that holds no secret and runs no
action (`gate` in the .NET workflow, the `Capture live Dependabot identity` step of `prepare` in
the Swift workflow) lists the PR's changed files through the API, fails closed on an API error or
a truncated listing, and sets `eligible=false` if any path starts with `.github/`. Every
downstream job, including the one whose step receives `DEPENDABOT_AUTOMERGE_TOKEN`, requires that
output. The gate is a job-level check, not `paths-ignore` or a negated `paths:` pattern: a negated
pattern still triggers when another changed file matches, and `paths` cannot be combined with
`paths-ignore`. A `github-actions` Dependabot bump therefore never reaches a write path, and the
ecosystem checks (`nuget` for .NET, the Skip classifier for Swift) independently reject it.
Because `.github/` is unchanged, the merge-ref workflow and local composite actions equal the
base's, and every `uses:` in the write-capable jobs is pinned to a full commit SHA (asserted by
tests). The remaining trust is in the actor, author, same-repo, branch, and `web-flow`-verified
head gates, the exact-diff and path allowlists, and the head lease. GITHUB_TOKEN is read-only in
every job (`contents: read`, `pull-requests: read`, `actions: read`); it never pushes.

Dependabot-triggered `pull_request` runs read only Dependabot secrets, not Actions secrets.
`DEPENDABOT_AUTOMERGE_TOKEN` must therefore exist as a Dependabot repository secret; it is the only
secret either workflow reads. Pushes made with the default GITHUB_TOKEN do not trigger workflow
runs, so the final push keeps that token so CI reruns on the repaired head.

The Swift Android repair workflow first checks the exact live head's API identity and
verification status.
Only a verified Dependabot-authored head committed by GitHub's `web-flow` identity proceeds to the
existing metadata signature verification.
Human-authored or unsigned repair commits use the ordinary reviewed PR path; they do not enable
trusted repair. API failures and malformed or mismatched commit responses still fail
closed. Substantive native validation remains separate from this eligibility decision.

After the initial live PR identity is verified, the prepare job records its exact base and head and
defaults eligibility to false before looking up commit verification. If that later lookup fails,
the prepare job fails and its downstream repair job remains blocked.

Read-only publishers revalidate the live Dependabot identity, base and head SHAs, raw Git change
types, candidate and artifact allowlists, artifact provenance, hashes, and the head lease. They
construct repair commits with temporary Git indexes; only the final `git push --force-with-lease`
receives `DEPENDABOT_AUTOMERGE_TOKEN` (its name predates the removal of the auto-merge workflow).
That Dependabot secret must stay scoped to the repository Contents and Pull requests access needed by these
single repair pushes. See
[Frozen-install policy](reference-dependency-updates-frozen-install-policy.md) for the authoritative
manifests and generated-output sets.
