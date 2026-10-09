# Voucha Native Clients reference

[Back to Voucha Native Clients](README.md)

## Code-quality harness

Install the pinned Swift toolchain, then run checks through mise from the **repo root**:

```sh
mise install
mise exec -- ./swift-clients/tooling/harness.sh
```

The default invocation runs all checks including `test` (`swift test` for `test-support/`, `core/`,
and `ui/`). On Linux, `test/ui` is skipped automatically because the UI package is SwiftUI-only;
`test/core` and `test/test-support` run. `build-android` is a Linux-only opt-in gate that
cross-compiles `core/` with the Swift Android SDK. Select specific checks with
`--checks <comma-list>`:

```sh
mise exec -- ./swift-clients/tooling/harness.sh --checks fmt,lint,lint-tests # format + lint only
mise exec -- ./swift-clients/tooling/harness.sh --checks periphery           # dead-code scan only
mise exec -- ./swift-clients/tooling/harness.sh --checks build-android        # Android core compile only
```

| Check           | Tool                                 | Tier | What it enforces                                                                         |
| --------------- | ------------------------------------ | ---- | ---------------------------------------------------------------------------------------- |
| `fmt`           | swiftformat `--lint`                 | 1    | Formatting matches `.swiftformat`                                                        |
| `lint`          | swiftlint `--strict`                 | 1    | Source rules in `.swiftlint.yml`                                                         |
| `lint-tests`    | swiftlint `--strict`                 | 1    | Test rules in `.swiftlint-tests.yml`, including 500-line file cap                        |
| `ast-grep`      | repo-local ast-grep                  | 2    | `core/` Foundation-only and auth-framework boundary rules (see `ast-grep-rules/swift-*`) |
| `build`         | `swift build`                        | 1    | Compile `core/` and `ui/` packages                                                       |
| `build-android` | `bash tooling/build-android-core.sh` | 1    | Cross-compile `core/` for Android on Linux                                               |
| `periphery`     | periphery scan                       | 1    | Unused private/internal declarations in `core/` and `ui/`                                |
| `generate`      | xcodegen                             | 2    | Validates `apps/*/project.yml` parses and produces a project                             |
| `test`          | `swift test`                         | 1    | Unit tests for `test-support/`, `core/`, and `ui/` packages                              |

**Dead-code scan** (`periphery`): `--retain-public` is set so public API is not flagged while the
generated iOS/iPadOS shell is outside the package scan and Android UI integration remains future
work. Periphery is the sole unused declaration/import gate across indexed `core/` and `ui/` targets
locally and in CI; imports of external modules that Periphery cannot index have no separate
analyzer. Both commands first build with SwiftPM's explicit `.build/periphery-index` path and then
pass its `out/Products/Debug/index/store` to Periphery. The macOS UI package uses Xcode's bundled
Swift compiler because the target needs Apple SwiftUI overlays absent from mise's standalone
toolchain; CI selects Xcode 26.6, while mise remains the pinned Swift 6.4 compiler for Core,
Linux, and Android.

CI runs pinned SwiftFormat and SwiftLint containers on Linux, while the macOS runner image owns
Periphery installation and native compiler checks. Linux owns portable core and `test-support`
tests. macOS still runs the full core suite so Darwin `URLSession`, Security/Keychain, core LCOV,
and Swift DTO parity are exercised; those jobs compile different stacks. See
[native CI test placement](../docs/development/native-ci-test-placement.md). The local harness uses
`vouchington-tooling`'s `with-host-lock` primitive for compiler-heavy commands, so Core, UI,
Periphery, and Android builds share the same per-user lock without copying a Filaments CI helper.
The local harness can run `fmt,lint,lint-tests,ast-grep,build,periphery,generate`.

Each package's `Package.resolved` is its canonical SwiftPM lock; the generated app projects consume
the UI lock. Build, test, and dead-code checks require pinned versions. To update dependencies
intentionally, run `swift package --package-path swift-clients/<core|ui|apps/android> update`, review and commit
the lock change, then rerun the strict checks. If the local shared SwiftPM cache is corrupt, run
`swift package purge-cache`;
CI disables the shared dependency cache instead. The cross-ecosystem source of truth for update
ownership, frozen commands, cache isolation, and audit evidence is the
[dependency-update policy](../docs/development/reference-dependency-updates-frozen-install-policy.md#swiftpm).

When planning Swift tests, batch all compatible changed Swift files in one
per-framework invocation: `pnpm exec no-mistakes tests plan swift --changed-file <file-a>.swift
--changed-file <file-b>.swift --format commands`; use `--format json` to inspect `fallback_reason`
when the planner widens to the whole suite target(s).

## Test selection

For a dependency-only change, compare the revisions instead of supplying only changed files:

```sh
pnpm exec no-mistakes tests plan swift --base origin/main --head HEAD --format json
```

Semantic planning compares `Package.swift` and `Package.resolved`, then follows local package edges
from core through UI and the Android package. Inspect `reasons`, `warnings`, and the `direct`,
`dependencies`, and `sample` groups independently: a missing baseline or unsupported dependency
form uses the selected environment's conservative policy and is never evidence that there are no
causal tests. The command can select `VouchaAndroidTests`; run its emitted command, or reproduce it
directly with `bash swift-clients/tooling/with-build-lock.sh swift test --package-path swift-clients/apps/android
--filter VouchaAndroidTests --force-resolved-versions`.

This is a local/pre-push planning aid only. GitHub's native Swift workflow intentionally remains
full-suite: it runs core, UI, and Android test/build coverage rather than consuming a narrowed
planner result. The Core and UI LCOV reports are checked with `pnpm run coverage:swift`; its
`.coverage-rules.yml` keeps app shells, package manifests, and opt-in integration tests exempt
while requiring 90% patch coverage for all other Swift sources.

`build/ui`, `periphery`, and `test/ui` are macOS-only until the UI package is Linux-portable.
`build/core`, `test/core`, and `test/test-support` run on Linux. `build-android` is Linux-only and
exists as a local opt-in compile gate for Android-via-Swift.

On a failure, the harness preserves its normal live combined output, then appends an execution-
ordered `failed checks:` table with the check name, classification, original exit status, integer
elapsed seconds, and the final 40 combined-output lines. `host-timeout` and `lock-timeout` require
the exact `expensive-build` wrapper marker and matching exit status; every other failure is
`check-failure`. See [Host Locks](../docs/development/host-locks.md#native-harness-timeout-classification).

## Gotchas

- **`CodingKeys` + `convertFromSnakeCase` silent failure**: if an enum case name doesn't snake-map
  to the backend string, `JSONDecoder` silently decodes to `.unknown` instead of failing. Always
  verify case names against the actual backend string values (backend strings are the source of
  truth — never rename a case without checking the API contract). Audit the backend schema whenever
  adding enum cases.
- **`@State` mutation in view body**: mutating `@State` directly in `body` creates a silent
  render loop. Only mutate state in response to user events or via bindings passed to child views.
- **macOS-vs-Linux portability**: `mktemp` behaves differently on Linux. The harness uses an explicit
  `$AST_GREP_BIN` when supplied; otherwise it uses only the repo-local
  `node_modules/@ast-grep/cli/ast-grep` binary. The `test/ui` check is Darwin-only. `test/core` and
  `test/test-support` run on Linux. Confirm shell recipes work there before relying on them
  locally.
- **Diagnosing an `xctest` crash (native SIGSEGV, not an assertion failure)**: `test-ui`'s CI job
  uploads failure-only `~/Library/Logs/DiagnosticReports/*.ips`/`*.crash` reports — see
  [native harness failure classification](../docs/development/host-locks.md#native-harness-timeout-classification)
  before treating the failure as transient. To force a deterministic repro of a suspected use-after-free, run
  under AddressSanitizer: `swift test --package-path ui --sanitize=address`.

## Development

Read [AGENTS.md](AGENTS.md) before implementation for native parity, coverage thresholds, portable
core boundaries, dependency, auth, API parity, and entitlement invariants.
