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
are not cached.

The Mac Catalyst NuGet package cache restores on every run but saves a missing key only from a
`main` push after the locked restores, rendered page tests, and app build succeed. Pull requests
still perform the same locked restore and tests on a cache miss; they do not upload a separate
multi-gigabyte cache copy. The bounded save is best-effort package reuse, not build-output or
toolchain reuse.

## pnpm

pnpm is not pinned. No `package.json` declares `packageManager`, `devEngines.packageManager`, or
`engines.pnpm`. Under such a pin, pnpm 12 writes `pnpm-lock.yaml` as two YAML documents, and
single-document readers such as Dependabot
([dependabot/dependabot-core#15904](https://github.com/dependabot/dependabot-core/issues/15904))
misread it. Unpinned, the lockfile stays one document.

- Locally, install any pnpm 12 release and run `pnpm` from `PATH`.
- In CI, every job or composite action that runs pnpm adds `pnpm/action-setup` directly after
  `actions/setup-node`. Its only input is `version: latest-12`, so the action runs
  `pnpm self-update latest-12` and CI uses the newest pnpm 12 release that is at least one day old.
  `self-update` honors pnpm's default one-day `minimumReleaseAge`: when the newest 12.x release is
  younger than that, it installs the newest mature one instead. New 12.x releases reach CI without
  a pull request; moving to pnpm 13 needs one.
- Do not pass a bare `version: 12`. That range matches the pnpm the action bundles (12.3.4 in
  v6.1.0), so the action skips `self-update` and keeps it. pnpm 12.3.0 through 12.4.2 make
  `pnpm dlx` exit 1 with `ERR_PNPM_IGNORED_BUILDS` for packages with build scripts, such as
  `wrangler`; 12.5.0 fixed it.
- The `/plan` and `/fix` gate jobs stay checkout-free: the action needs no `package.json`, and
  those jobs only run `pnpm dlx`.
- Workflows, scripts, and the native lock wrappers run `pnpm` from `PATH`. Nothing bootstraps pnpm
  through Corepack or a versioned `npx pnpm@<version>` call.
- Dependabot runs the pnpm bundled with its updater image.

[`tests/pnpm-setup.test.mjs`](../../tests/pnpm-setup.test.mjs) enforces these rules, including one
action release and one `latest-<major>` across every call site. To change the major, update every
`pnpm/action-setup` call in the same change.

## Android validation

Swift Android native compilation and APK assembly run as an exact-run producer and consumer on
separate macOS VMs. Host Swift package tests and native test-library preparation run in parallel
with the producer. The producer packages only bounded generated build state, tied to the verified
candidate revision, run, attempt, and pinned Gradle version. The consumer verifies its hash and
identity before rehydration, then executes the original app and instrumentation APK task closure.
The consumer logs the native Swift task outcome to measure actual reuse; the pinned task does not
document an `UP-TO-DATE` outcome as its correctness condition. Extracted Swift,
Android SDK, and NDK toolchains are materialized independently in each job and never included in
the artifact. The stable `Swift Android Skip` check requires producer, consumer, host tests,
test libraries, and the cookie runtime proof to succeed; `Tests` still requires that result.
Local Android validation continues to default to both original phases. Producer and consumer
job budgets are 30 minutes while hosted transfer and cold-build timings are measured. These
budgets are safety bounds, not evidence that the under-ten-minute target has been met.
The dedicated host job runs the planner-selected source tests with
`run-android-host-swift-tests.sh --package-path swift-clients/apps/android --filter
VouchaAndroidTests` on Xcode 26.6 with Swift 6.3.3. Swift 6.4 remains on the Android Gradle
path, and the later emulator instrumentation uses the exact candidate APKs. The Gradle producer
and consumer record pinned `--profile` summaries. The consumer selects the profile named by its
own Gradle invocation because the verified state also contains the producer's prior profile.
Reported task and category durations may overlap and are not a critical-path sum.

The Android archive and Gradle caches restore by pinned input keys on every run. Gradle's
task-output build cache is separate from its user home: local builds use the canonical Darwin
temporary directory, while GitHub Actions uses a private stable runner-home path because cache
archives retain absolute paths across hosts. The native producer and app consumer restore the
task-output cache. Test-library validation uses SwiftPM without Gradle, so it does not download
that cache. Both Gradle jobs use the same
checked task-output path that `pre-push.sh` gives Gradle. Only a `main` push writes a missing key:
the test-library phase saves the shared checksummed-download archive after SDK materialization,
and the app consumer saves its Gradle user home and bounded task-output cache after APK upload. The
task-output save accepts at most 512 MiB of logical regular-file bytes, 100,000 members, and a
30-second scan; an oversized cache simply stays job-scoped. Full app tasks and candidate APK
checks remain required on hits.
Pull requests still compile from source on a miss, but do not write large per-PR cache copies
that compete with the default-branch entries. The archive key follows the checksum-bearing
materialization script, so an unrelated workflow edit does not invalidate verified downloads.
The task-output key partitions runner OS, architecture, canonical cache path, pinned Swift/Skip/
Gradle configuration, and candidate revision. A pull request reads only its exact base revision's
cache. A `main` push first tries its own revision (for reruns), then its exact predecessor; after
a successful APK upload, it writes a bounded snapshot for the new revision if that key was absent.
There are no prefix restore keys. A missing or failed predecessor gives a cold build. The first
default-branch entry can only be written by a successful `main` push, so an open pull request
cannot establish warm-hit timing for this new key.

The prior pinned Gradle profile baseline measured task overlap and bounded generated-state
sizes. This experiment measures compression, artifact transfer, rehydration, and repeated Gradle
configuration on hosted runners. Only generated build outputs cross the exact-run artifact;
stable package/download caches and job-scoped extracted toolchains keep their existing rules.

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
`dependabot-automerge.yml` repairs Android Skip's checked archive version and checksum before
enabling auto-merge. Their `pull_request_target` producers check out only the exact default-branch
base and fetch candidate dependency files as inert API data. They never check out or execute the
Dependabot head. Fork pull requests run on hosted `pull_request` without repository secrets; do not
reintroduce self-hosted labels for untrusted code.

The auto-merge workflow first checks the exact live head's API identity and verification status.
Only a verified Dependabot-authored head committed by GitHub's `web-flow` identity proceeds to the
existing metadata signature verification.
Human-authored or unsigned repair commits use the ordinary reviewed PR path; they do not enable
trusted repair or auto-merge. API failures and malformed or mismatched commit responses still fail
closed. Retarget cleanup and substantive native validation remain separate from this eligibility
decision.

After the initial live PR identity is verified, the prepare job records its exact base and head and
defaults eligibility to false before looking up commit verification. If that later lookup fails,
the prepare job fails and its downstream repair and auto-merge jobs remain blocked; a separate
`always()` cleanup may still disable inherited auto-merge after it independently revalidates the
same open Dependabot PR and head. It skips cleanly when the REST response says `auto_merge: null`,
and rejects a missing or malformed `auto_merge` value. The cleanup uses
`DEPENDABOT_AUTOMERGE_TOKEN`; its fresh identity check narrows the race but is not an atomic
compare-and-disable operation.

Read-only publishers revalidate the live Dependabot identity, base and head SHAs, raw Git change
types, candidate and artifact allowlists, artifact provenance, hashes, and the head lease. They
construct repair commits with temporary Git indexes; only the final `git push --force-with-lease`
receives `DEPENDABOT_AUTOMERGE_TOKEN`. That secret must stay scoped to the repository Contents and
Pull requests access needed by Dependabot auto-merge and these single repair pushes. See
[Frozen-install policy](reference-dependency-updates-frozen-install-policy.md) for the authoritative
manifests and generated-output sets.
