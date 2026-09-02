# Native CI test placement

Run each test on the cheapest self-hosted runner that can execute the compiled code under test.
Do not add a second operating system for confidence. Dual-OS is required only when the runtime or
`#if` slice is actually different.

## Default placement

| Workload                                                                  | Runner                                | Why                                                                       |
| ------------------------------------------------------------------------- | ------------------------------------- | ------------------------------------------------------------------------- |
| Portable .NET (`Voucha.DotNet.sln`), Core patch coverage, .NET DTO parity | `[self-hosted, Linux, Docker, Tests]` | Same test code, no meaningful Linux-vs-macOS branches                     |
| Portable Swift core + `test-support`                                      | `[self-hosted, Linux, Docker, Tests]` | FoundationNetworking, Glibc sockets, Linux cookie storage, `swift-crypto` |
| Swift core Darwin/Security slice, core LCOV, Swift DTO parity             | `[self-hosted, macOS, Tests]`         | Darwin `URLSession`, Security/Keychain, `xcrun llvm-cov`                  |
| Swift UI, Periphery, Android Skip, macOS app smoke                        | `[self-hosted, macOS, Tests]`         | SwiftUI, Xcode, Skip host tools                                           |
| Swift Android core compile                                                | `[self-hosted, Linux, Docker, Tests]` | Cross-compile in the pinned Swift container                               |
| .NET MAUI App tests and Mac Catalyst smoke                                | `[self-hosted, macOS, Tests]`         | MAUI workload and Mac Catalyst RID                                        |

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

See [CI runners](ci-runners.md) for label and cleanup policy.
