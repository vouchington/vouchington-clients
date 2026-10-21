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
required Xcode version is unavailable. A missing toolchain must not turn an unrun app build green.

Do not add `self-hosted`, other `windows-*`, or unlisted `ubuntu-*` / `macos-*` labels. The three
Windows jobs run only for their relevant source/toolchain changes; Windows app packaging and signing remain out of scope.

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

Swift Android APK assembly and native test-library preparation run in parallel on separate
macOS VMs. Both phases retain the verified contract setup and host Swift package tests;
they do not share a SwiftPM build directory. Each phase runs the planner-selected Android host
source-test command, `run-android-host-swift-tests.sh --package-path swift-clients/apps/android
--filter VouchaAndroidTests`, on Xcode 26.6 with Swift 6.3.3. That command is source-test
evidence. The Swift 6.4 Gradle APK build and the later emulator instrumentation are separate.
The stable `Swift Android Skip` check requires both phases to succeed, and the aggregate `Tests`
check requires that result. Local Android validation defaults to both phases; `pre-push.sh
--phase app` and `--phase test-libs` select individual phases for CI. This compiles the app and
packages Android test libraries without a device; it does not claim device test execution.
The shared job budget is 60 minutes so an app-phase Gradle user-home cache miss can finish Skip
compilation and debug APK packaging. Test-library preparation finishes well inside that budget.

The cookie restart runner bounds emulator shutdown after the instrumentation phases. It stops
and reaps only its captured emulator child, including a child that ignores termination or is
stopped. Cleanup preserves an earlier test failure and fails an otherwise successful run if the
owned emulator cannot be stopped. Host regression tests cover shutdown behavior; they do not
replace emulator instrumentation.

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
