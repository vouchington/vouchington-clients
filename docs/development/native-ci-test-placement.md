# Native CI test placement

Run each test on the cheapest GitHub-hosted runner that can execute the compiled code under test.
Do not add a second operating system for confidence. Dual-OS is required only when the runtime or
`#if` slice is actually different.

## Default placement

| Workload                                                                                             | Runner           | Why                                                                                |
| ---------------------------------------------------------------------------------------------------- | ---------------- | ---------------------------------------------------------------------------------- |
| Portable .NET (`Voucha.DotNet.sln`), Core patch coverage, .NET DTO parity                            | `ubuntu-latest`  | Same test code, no meaningful Linux-vs-macOS branches                              |
| Portable Swift core + `test-support`                                                                 | `ubuntu-latest`  | FoundationNetworking, Glibc sockets, Linux cookie storage, `swift-crypto`          |
| Swift core full suite (including portable tests), Darwin/Security slice, core LCOV, Swift DTO parity | `macos-latest`   | Full core suite exercises Darwin `URLSession`, Security/Keychain, `xcrun llvm-cov` |
| Swift UI, Periphery, Android Skip, macOS app smoke                                                   | `macos-latest`   | SwiftUI, Xcode, a logged-in macOS GUI session, and Skip host tools                 |
| Android encrypted cookie restart instrumentation                                                     | `ubuntu-latest`  | Runs the candidate Android APKs on one API 35 Google APIs emulator with KVM        |
| iOS Simulator app launch smoke                                                                       | `macos-latest`   | Generated Xcode project, iOS Simulator SDK, and actual app launch                  |
| Swift Android core compile                                                                           | `ubuntu-latest`  | Cross-compile in the pinned Swift container                                        |
| .NET MAUI App tests and Mac Catalyst smoke                                                           | `macos-latest`   | MAUI workload and Mac Catalyst RID                                                 |
| .NET Windows local-image decoder                                                                     | `windows-latest` | `#if WINDOWS` WinRT `BitmapDecoder` and `BitmapEncoder` implementation             |
| .NET Windows language-model surface                                                                  | `windows-latest` | Pinned Windows App SDK 1.8 WinMD admission and answer-headroom probe               |

Linux rows are the cheapest job that can run that work. They do not make a second OS optional when
Required dual-OS says that suite also compiles a different stack.

The macOS app smoke job builds the generated macOS app, then launches it with the existing
root-shell XCUITest. The build stays unsigned. The launch deletes that unsigned product and signs
ad hoc, with no Team ID, using `VouchaLaunchSmoke.entitlements`: sandbox and outgoing network
only. Associated domains, Sign in with Apple, and the team keychain group stay on the shipping
entitlements because they make an ad-hoc signature invalid. A preflight refuses to
continue when WindowServer is down or the runner user does not own the GUI console, and the job
requires exactly one passed test with nothing skipped. That is launch evidence for the root shell
only; sign-in and tab coverage stay on #80.

The iOS build jobs compile the arm64 test bundle for an available iPhone Simulator and the x86_64
app slice in parallel on `macos-latest`. Xcode exports the arm64 tests as a portable
`.xctestproducts` bundle. Separate iPhone and iPad launch jobs download and verify that same
run/attempt/candidate/Xcode/SDK-bound artifact and execute it using `-testProductsPath` and
`test-without-building`; neither launch job recompiles. The iPhone job requires exactly two passed,
non-skipped root-shell and real Videos-tab tests. The iPad job requires exactly one passed,
non-skipped root-shell test on an available iPad Simulator. The native `Tests` fan-in requires
both device-family launches and the x86_64 compile. The DEBUG fixture restores its account
through `SessionManager.refresh`; it does not prove sign-in, cookie persistence, or CAPTCHA
behavior. Both jobs receive the complete fixture checkout explicitly through the generated scheme
and fail closed when a fixture or Simulator is unavailable.
Both launch jobs are required by the native workflow `Tests` fan-in; skipped cases and test retries are not
accepted as feed-flow evidence. This is partial coverage of #77. iPad feed flow, sign-in, and
Catalyst remain outstanding.

Run `pnpm test` when changing native workflow routing or selected app tests. The quality command
and native test planners do not execute the Node contract assertions that check those selectors,
result predicates and fixture-root wiring.

Dependabot metadata edits use `dependabot-edited.yml`, with one conditional call to the existing
auto-merge worker. Ordinary human PR title/body edits produce one skipped router check; trusted
Dependabot PR edits retain metadata verification and non-default-base cleanup. The gate uses the
PR author rather than the editing actor so maintainer retargets still run. Lifecycle/head events
remain on `dependabot-automerge.yml`. Only the worker owns concurrency, with the original shared
group across both entrypoints. The caller passes the existing auto-merge secret explicitly and
grants read access for the worker's same-run repair artifact download.

This bounds metadata noise; it does not eliminate workflow/check records. GitHub supports event,
base-branch and path filters, while [job conditions still create skipped checks][job-conditions].
Zero records for ordinary human edits would require an authenticated external webhook or GitHub
App to filter before dispatch. Routing tests verify the source wiring; actual skipped-caller check
counts require a hosted observation after the base-owned workflow reaches `main`.

[job-conditions]: https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-jobs-with-conditions

## Required dual-OS

Swift core portable tests run on Linux **and** macOS. That is not duplicate work:

- Linux compiles `FoundationNetworking` and the non-Security fallbacks (`AppAttestKeyStore` in-memory
  stub, Linux cookie storage). Failures such as URLProtocol header capture and `URLSession.download`
  illegal instruction only appear there.
- macOS compiles Darwin `URLSession`, Security/Keychain, and the `#if canImport(Darwin)` test
  methods. Core LCOV (`xcrun llvm-cov`) and Swift DTO parity stay on this job because they need
  that binary.

Darwin-only and Security-only tests are compiled out on Linux (`#if canImport(Darwin)` /
`#if canImport(Security)`). They are macOS-only, not a second copy of the Linux suite.

## Forbidden dual-OS

Portable .NET tests must not run on macOS. HttpClient and the Core suite do not change by OS in
this repository. Coverage and DTO parity are Linux-only extras on the same job, not a reason to
keep a second OS.

The Windows decoder test is a separate, changed-file-routed test project that links only the
Windows image decoder helper. It runs when the helper, its consumer/tests, pinned SDK or package
graph, and its CI routing/policy change. It does not build or test the full MAUI Windows app.
The isolated mise SDK does not install workloads, while the repository's `global.json` pins a
workload manifest version. This workload-free project passes `MSBuildEnableWorkloadResolver=false`
only to its locked restore and test commands so MSBuild does not fail while probing absent
workload manifests; the Windows target and WinRT decoder tests still compile and run.

The Windows language-model surface probe is a separate changed-file-routed test project. It
restores the same `Microsoft.WindowsAppSDK` 1.8.260710003 pin as the app, compiles projection
bindings for `GetUsablePromptLength`, `CreateContext`, `GenerateResponseAsync`, and
`LanguageModelOptions`, and reads the restored `Microsoft.WindowsAppSDK.AI` 1.8.79 WinMD. The
hosted job records prompt-length admission and the absence of an answer-token reservation member.
It does not call the model, implement the member MCP loop, or stand in for supported-device
generation. It uses the same workload-resolver opt-out as the decoder test.

Do not add `strategy.matrix.os` (or a second job that runs the same `dotnet test` / `swift test`
filter) unless the new OS compiles different source or links a different networking stack.

## Adding a test

1. Put it on Linux unless it needs Darwin, Security, SwiftUI, MAUI, or Xcode.
2. If it cannot run on Linux, wrap the test (or file) in `#if canImport(Darwin)` or
   `#if canImport(Security)` so Linux does not compile a skip-stub.
3. Collect coverage on the job that executes the compiled slice. Do not collect the same LCOV
   twice.
4. Advisory DTO fixture parity stays on the job that already builds that client test bundle
   (Linux .NET, macOS Swift).

See [CI runners](ci-runners.md) for the hosted label allowlist.

The Windows MAUI app compiles on `windows-latest` when .NET source, dependency or toolchain files change. This checks the actual `#if WINDOWS` and WinUI build graph; portable Core tests remain Linux-only. It uses committed Windows RID locks and the mise-managed SDK/workload set, with signing and packaging disabled.
