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
| Swift UI, Periphery, Android Skip, macOS app smoke                                                   | `macos-latest`   | SwiftUI, Xcode, Skip host tools                                                    |
| Android encrypted cookie restart instrumentation                                                     | `ubuntu-latest`  | Runs the candidate Android APKs on one API 35 Google APIs emulator with KVM        |
| iOS Simulator app launch smoke                                                                       | `macos-latest`   | Generated Xcode project, iOS Simulator SDK, and actual app launch                  |
| Swift Android core compile                                                                           | `ubuntu-latest`  | Cross-compile in the pinned Swift container                                        |
| .NET MAUI App tests and Mac Catalyst smoke                                                           | `macos-latest`   | MAUI workload and Mac Catalyst RID                                                 |
| .NET Windows local-image decoder                                                                     | `windows-latest` | `#if WINDOWS` WinRT `BitmapDecoder` and `BitmapEncoder` implementation             |

Linux rows are the cheapest job that can run that work. They do not make a second OS optional when
Required dual-OS says that suite also compiles a different stack.

The iOS job first compiles against a generic iOS Simulator destination on `macos-latest`, then
runs the shared UI launch-smoke target on an available iPhone Simulator with signing disabled.
It fails when no iPhone Simulator is available or the app does not render its root shell, and is
required by the native workflow `Tests` fan-in.

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
