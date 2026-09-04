# Voucha .NET Clients

This workspace contains the .NET client stack for Voucha. The product target is Windows desktop, and the MAUI app also ships a Mac Catalyst head so posts, referral links, topics, and auth flows can be exercised on macOS during development and CI.

## Current Status

- .NET SDK and workload set: the repository-root [`global.json`](../global.json) is the sole
  policy source; it uses `rollForward: latestPatch` and pins the workload set too.
- Portable solution: `Voucha.DotNet.sln`.
- MAUI app targets: `net10.0-maccatalyst` and `net10.0-windows10.0.26100.0`.
- Rendered capability areas include authentication and account settings with household and
  payment-card management, news and sources, posts and compose, topics including native
  topic/source import and export plus signed-in top-hashtag recommendations and administrator
  mapping actions, profiles and friends, notifications, direct messages and support,
  lists, referral and landing pages, media playback, search, and role-gated staff operations. This is not
  a claim of web parity; see the Vouchington [client parity matrix](https://github.com/vouchington/vouchington/blob/main/docs/requirements/CLIENT-PARITY-MATRIX.md)
  and [machine-readable evidence contract](https://github.com/vouchington/vouchington/blob/main/docs/requirements/client-feature-parity.json)
  for exact capability status and tracked gaps.
- API core: typed fixture-backed helpers plus Swift/web-route-parity endpoint factories under `src/Voucha.Client.Core/Api`.
- Auth core: cookie-backed session state, email OTP, MFA TOTP, dev-cookie injection, and a passkey assertion seam for platform implementations.
- Deep linking: `voucha` protocol activation on Windows and Mac Catalyst, with MAUI lifecycle hooks and an app-shell coordinator that selects existing native targets or opens Session for login links.
- Post compose: native signed-in post/community-post creation for discussions, reviews, data points, links, articles, and blogs. CAPTCHA-gated submissions open the shared MAUI Turnstile challenge and send the resulting token; local development can opt into omitting it with `VOUCHA_POST_COMPOSE_CAPTCHA_BYPASS=true`, which only succeeds against backends already configured with `SKIP_CAPTCHA_VERIFICATION=true`.
- CI: portable restore/build/test plus Core changed-line coverage at 80% on Linux, and rendered
  MAUI page tests plus a Mac Catalyst smoke build on macOS. Do not run `Voucha.DotNet.sln` on macOS.
  App coverage is explicitly exempt because the platform app shell is not instrumentable by
  Coverlet. See [native CI test placement](../docs/development/native-ci-test-placement.md).
- Local models: Windows uses the Windows system language model through the Windows App SDK, while
  both heads support UUID-scoped OpenAI-compatible endpoint profiles. Windows model setup is an
  explicit user action, and local failures restore the draft without automatic fallback.

## Local checks

Run the local .NET harness from the repo root:

```sh
./dotnet-clients/tooling/harness.sh
```

For individual checks, run from the repository root:

```sh
dotnet restore --locked-mode dotnet-clients/Voucha.DotNet.sln
./dotnet-clients/tooling/harness.sh --checks build
dotnet test dotnet-clients/Voucha.DotNet.sln --configuration Release --no-build
dotnet format dotnet-clients/Voucha.DotNet.sln --verify-no-changes --no-restore
./dotnet-clients/tooling/with-build-lock.sh dotnet build dotnet-clients/src/Voucha.Client.App/Voucha.Client.App.csproj --configuration Release --framework net10.0-maccatalyst --no-restore
./dotnet-clients/tooling/harness.sh --checks ast-grep
./dotnet-clients/tooling/harness.sh --checks resx-path
```

The harness restores the portable solution in locked mode, verifies formatting, runs the ast-grep
presentation and Core-boundary scans, proves generated RESX satellite resources still load when the
MSBuild artifacts path contains an apostrophe, and builds in Release. It ignores an ambient
`ArtifactsPath` so concurrent worktrees keep build outputs isolated, including when the checkout
path contains spaces or apostrophes. Full portable tests, rendered MAUI page tests, and MAUI smoke
builds remain in CI. The rendered page tests require the pinned MAUI workload, run separately from
the portable solution, and do not require the exact Xcode version used by the Mac Catalyst smoke
build.

Before `--exec` and any selected restore, format, RESX-path, or build check, the harness resolves
the first `dotnet` host on `PATH` and validates it once from the repository root. The same absolute
host then runs direct .NET commands, its directory is exported as `DOTNET_ROOT`, and that directory
leads `PATH` for child scripts. If that host is missing or cannot satisfy the root `global.json`,
the harness replays each resolver output, preserves the failing exit status, and stops before a
command or build lock starts.

Outside GitHub Actions, a failed PATH host may fall back to `$DOTNET_ROOT/dotnet` when that
variable was already set, then `$HOME/.dotnet/dotnet` (the Microsoft user-local layout). Those extra
roots are skipped when `GITHUB_ACTIONS` is set so a persistent runner cannot hide a broken
job-scoped install behind a leftover `$HOME` SDK. The harness does not walk the rest of `PATH`,
search Homebrew prefixes, or install SDKs.

Homebrew's `/opt/homebrew/bin/dotnet` only sees SDKs registered in that install. A Microsoft
user-local `10.0.3xx` SDK at `$HOME/.dotnet` is invisible to it, and non-interactive shells often
omit `$HOME/.dotnet` from `PATH` because they do not load `~/.zshrc`. Put a compatible SDK selected
by [`global.json`](../global.json) first on `PATH`, or keep the official installer at
`$HOME/.dotnet` for the local fallback. An ast-grep-only run remains SDK-independent. The external
RESX fixture receives a runtime copy of the root policy so its temporary project cannot escape SDK
selection.

## Dependency-aware test planning

For ordinary source edits, batch compatible changed files with `no-mistakes`. For a dependency
manifest or lock change, compare the semantic base and head revisions:

```sh
pnpm exec no-mistakes tests plan dotnet --base origin/main --head HEAD --format json
```

The planner traces `Directory.Packages.props`, project `PackageReference` and `ProjectReference`
edges across Core, App, Core.Tests, and App.Tests, plus all seven committed generic and
Mac Catalyst RID-specific package locks.
Inspect causal `reasons` separately from the `sample` group. If JSON reports a missing baseline or
unsupported MSBuild/dependency form, follow the configured conservative policy rather than treating
an empty causal group as proof that no tests are affected. App.Tests is outside
`Voucha.DotNet.sln`; run any emitted App test command explicitly, or reproduce it with:

```sh
dotnet restore dotnet-clients/tests/Voucha.Client.App.Tests/Voucha.Client.App.Tests.csproj --locked-mode
./dotnet-clients/tooling/with-build-lock.sh dotnet test dotnet-clients/tests/Voucha.Client.App.Tests/Voucha.Client.App.Tests.csproj --configuration Release --no-restore
```

This affects local and pre-push planning only. GitHub's native .NET workflow intentionally remains
full-suite: the Linux portable job tests `Voucha.DotNet.sln` once, while the macOS MAUI job builds
and tests `Voucha.Client.App.Tests` separately and verifies the native lock inventory.

CI collects LCOV from the portable Core test suite and runs the published `coverage-check` policy
engine against `.coverage-rules.yml`, requiring `lcov.info` and failing empty or missing reports.
The rendered App test project remains a documented 0% exemption: MAUI's platform shell is compiled
and smoke-tested on Mac Catalyst, but Coverlet cannot instrument it reliably. Use
`COVERAGE_BASE=origin/main COVERAGE_HEAD=HEAD pnpm run coverage:dotnet-core` after producing
`dotnet-clients/TestResults/core/lcov.info`. Local compiler-capable commands may use
`with-build-lock.sh`; it waits up to 60 seconds and fails closed. GitHub Actions does not wrap
those commands. `dotnet test --no-build` remains outside that lock.

NuGet restores use nuget.org as the only package source. The SDK's local `library-packs` and
fallback folders are disabled because different .NET distributions can contain packages with the
same version but different content hashes. Local and CI runs reuse per-user package, HTTP, and
plugin caches under `${XDG_CACHE_HOME:-$HOME/.cache}/voucha/dotnet` (override with
`VOUCHA_DOTNET_CACHE_ROOT`). Per-run scratch state stays under `VOUCHA_DOTNET_TEMP_ROOT`, and the
harness environment replaces inherited NuGet/MSBuild routing so hostile host paths cannot leak
into the build.

The harness suppresses successful check output and replays complete failed-check output. It then
ends failures with an execution-ordered `failed checks:` table containing each check name,
classification, original exit status, integer elapsed seconds, and its final 40 combined-output
lines. `host-timeout` and `lock-timeout` require exact `expensive-build` or
`host-package-manager` wrapper markers with their matching exit statuses; generic host-pressure
diagnostics remain `check-failure`. See
[Host Locks](../docs/development/host-locks.md#native-harness-timeout-classification).

When changing a MAUI dependency, regenerate both app and Core Mac Catalyst locks for `maccatalyst-arm64` and `maccatalyst-x64` with `dotnet restore --force-evaluate` and the corresponding `TargetFramework` and `RuntimeIdentifier` properties. Regenerate from an empty `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH`, then repeat every restore with `--locked-mode` and confirm the lockfiles remain unchanged. SDK and workload updates must change both pins in the repository-root `global.json` together before regenerating the locks.

Follow the repository [frozen-install policy](../docs/development/reference-dependency-updates-frozen-install-policy.md#nuget)
for dependency review and audit evidence. For the complete seven-lock matrix, use
`bash dotnet-clients/tooling/restore-locks.sh update` followed by
`bash dotnet-clients/tooling/restore-locks.sh verify` with isolated `NUGET_PACKAGES` and
`NUGET_HTTP_CACHE_PATH`, `NUGET_PLUGINS_CACHE_PATH`, and `NUGET_SCRATCH`, while keeping lock-restore
MSBuild artifacts in an isolated checkout-local tree. Dependabot uses that trusted macOS path only for literal central package
versions; SDK/workload and `MauiVersion` updates remain manual, and repaired NuGet PRs require
manual merge.
The restore helper partitions that artifact tree into portable, app-test, and per-RID graphs so a
later restore cannot overwrite the assets consumed by a subsequent `--no-restore` build.
Unlike ordinary development, this lock boundary uses a fresh job-scoped SDK root and requires the
exact `sdk.version` declared in the repository-root `global.json`; this prevents `latestPatch` from selecting a newer SDK
left by an earlier job on a persistent runner.

Windows packaging can be built locally on Windows. The macOS app head is the supported compile-check proxy on the current runner fleet.

## Structure

- `src/Voucha.Client.Core/` contains portable models, API services, auth/session services, and view models.
- `src/Voucha.Client.App/` contains the thin .NET MAUI shell for Mac Catalyst and Windows. It is
  built separately from the portable solution after installing the MAUI workload.
- `tests/Voucha.Client.Core.Tests/` covers portable client behavior.
- `tests/Voucha.Client.App.Tests/` covers rendered MAUI page behavior and is intentionally excluded from the portable solution because it requires the MAUI workload.
- `FeatureFlagOverridesViewModel` owns device-local override reads, administrator/developer access,
  persistence, and immediate effective-state publication. Its local mutations never call Dynamic
  Config endpoints or open a web surface. `FeatureFlagOverridesPage` is a standalone native entry
  from the role-aware Engineering hub, separate from `DynamicConfigPage`.

## Configuration

Release builds default to `https://voucha.ai`. Set `VOUCHA_API_BASE_URL` and `VOUCHA_WEB_BASE_URL`
for local development. Set `VOUCHA_TURNSTILE_SITE_KEY` when testing CAPTCHA-gated flows; app startup
must remain safe when the key is absent.

## Agent rules

Read [CLAUDE.md](CLAUDE.md) before implementation for native parity, portable-core boundaries,
session storage, TLS pinning, and file-size invariants.
