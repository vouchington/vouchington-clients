# Linux agent setup for portable native work

This setup supports editing both clients and running their portable Core tests on Linux. SwiftUI,
Xcode generation, Periphery, MAUI App tests, Mac Catalyst smoke, and Darwin/Security tests still
require macOS. The [CI placement policy](native-ci-test-placement.md) explains the split.

The host or machine profile must first provide Node.js 26+, pnpm 12, Docker, mise, and a .NET SDK
satisfying the root [global.json](../../global.json) (currently 10.0.301 with latest-patch roll
forward). Docker runs the same pinned Swift 6.3.3, SwiftFormat, and SwiftLint images as CI, so a
host Swift installation is unnecessary for these portable checks. Use an x86_64 Linux host if you
also need the opt-in Android Core cross-compile: its NDK path is currently
`toolchains/llvm/prebuilt/linux-x86_64/bin/clang`.

Supply the path to a **complete** Vouchington checkout explicitly. The repository scripts do not
discover or fetch one. Choose an absent or empty output directory outside both checkouts:

```sh
./dev/setup-linux \
  --producer-root /absolute/path/to/vouchington \
  --stage-root /absolute/path/to/native-contract-stage
```

Setup verifies host versions and Docker, runs `pnpm install --frozen-lockfile`, installs the
checkout's pinned mise tools, restores the portable .NET solution in locked mode, stages the
producer's native exporter against this client checkout, and runs `contracts:check`. It never
rewrites generated localization. If the exporter reports a missing package, install the producer
checkout's frozen dependencies there explicitly and rerun setup with a new empty stage. The stage
root must then be passed explicitly to each fixture
check or test process:

```sh
./dev/linux-doctor --stage-root /absolute/path/to/native-contract-stage
./dev/linux-portable-tests --stage-root /absolute/path/to/native-contract-stage
./dev/linux-quality
```

`linux-portable-tests` runs the CI-aligned Swift `test-support` and Core tests in the pinned
container and .NET `Voucha.DotNet.sln` Core tests on the host. The Linux Swift Core invocation
excludes the fixture round-trip method that uses Darwin-only `XCTContext`; the macOS job runs that
method. `linux-quality` invokes the canonical `pnpm run quality:check`. On Linux, the Swift harness
runs pinned SwiftFormat and SwiftLint containers without network or checkout write access; on macOS,
it keeps native tooling. The pre-push hook uses this same lint path, so a host SwiftLint installation
is not required.

For changed-file planning, use `pnpm run test:plan:swift --base origin/main --head HEAD` and
`pnpm run test:plan:dotnet --base origin/main --head HEAD`. For uncommitted changes, pass one or
more `--changed-file` arguments. A planner can select SwiftUI or MAUI App tests: send those
commands to a Mac rather than claiming a Linux skip as coverage. Do not treat a missing baseline,
missing contract stage, or unavailable tool as an empty test selection.
