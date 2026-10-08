# Voucha Native Clients reference

[Back to Voucha Native Clients](README.md)

## Status

- **macOS**: Active native client with authenticated member, content, messaging, settings, media,
  and role-gated staff surfaces
- **iOS/iPadOS**: Generated app shell sharing the macOS SwiftUI capability sources; its CI launch
  smoke remains disabled pending #6705
- **Android**: A [Skip Fuse 1.9+ shell](apps/android/README.md) cross-compiles with the Swift Android SDK,
  uses ML Kit Prompt API for explicit-download on-device generation, and stores endpoint secrets
  with SkipKeychain. The effective floor is Android API 28. Play Store, Play Integrity, and broader
  product parity remain tracked in #6745 and #6620.

See the Vouchington [client parity matrix](https://github.com/vouchington/vouchington/blob/main/docs/requirements/CLIENT-PARITY-MATRIX.md)
and its [machine-readable evidence contract](https://github.com/vouchington/vouchington/blob/main/docs/requirements/client-feature-parity.json)
for current capability status and tracked gaps.

## Requirements

- macOS 14 (Sonoma) or later
- Xcode 16+
- xcodegen 2.42+
- swiftformat 0.54+
- swiftlint 0.57+
- periphery 3.0+
- Skip 1.9+ plus its Swift Android SDK (`skip android sdk install`) for the Android shell

The macOS installer in
[vouchington-github-actions-runners](https://github.com/vouchington/vouchington-github-actions-runners)
owns these host tools. Run its installer first, then run `./tooling/bootstrap.sh` to verify the
required versions.

## Quick start

```sh
cd swift-clients
./tooling/bootstrap.sh        # verify tool versions
./tooling/generate.sh macOS   # generate .xcodeproj from project.yml
./tooling/generate.sh iOS     # generate the iPhone/iPad project
open apps/macOS/Voucha.xcodeproj
open apps/iOS/Voucha.xcodeproj
swift test --package-path apps/android
cd apps/android && skip android build --android-api-level 28
```

By default the native clients use `https://voucha.ai`. For a local backend, set
`VOUCHA_API_BASE_URL` in the ignored `swift-clients/apps/Voucha.local.xcconfig` to that backend's
URL. In XCConfig syntax, use the generated file's slash variable so `//` is not parsed as a comment:

```xcconfig
VOUCHA_URL_SLASH = /
VOUCHA_API_BASE_URL = http:$(VOUCHA_URL_SLASH)$(VOUCHA_URL_SLASH)localhost:3000
```

Generated Debug schemes reference the file, so changing the URL does not require project
regeneration. Clean generation creates a production-default XCConfig when it is absent. See
[web-mode resource allocation](https://github.com/vouchington/vouchington/blob/main/dev/reference-resource-allocation-web-mode.md)
for the backend port contract. Set
`VOUCHA_TURNSTILE_SITE_KEY` (or shared `NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY`) when testing
CAPTCHA-gated flows against an environment with a Cloudflare Turnstile widget — native clients
intentionally do not keep a checked-in site-key fallback because Cloudflare registers staging and
production widgets separately. App startup must not fail solely because the site key is absent. In
Xcode: Product → Scheme → Edit → Run → Arguments → Environment Variables.

## Workflow

CI runs the macOS app-shell build smoke (`xcodebuild build` in `build-macos-app` in
`tests-swift-clients.yml`) and cross-compiles `core/` for Android in the Linux Docker
`build-android-core` gate via `./tooling/harness.sh --checks build-android`. `build-ios-app` stays
commented out pending #6705 (runner CoreSimulator/Xcode fix), so iOS Simulator-only failures do not
fail PRs; the iOS launch-smoke target is wired in `project.yml` for later enablement.

Run lock-aware package and app checks from the repository root:

```sh
mise exec -- ./swift-clients/tooling/harness.sh --checks build
mise exec -- ./swift-clients/tooling/harness.sh --checks test
./swift-clients/tooling/generate.sh macOS
(
  XCODE_BUILD_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/voucha-xcode-build.XXXXXX")"
  trap 'rm -rf "$XCODE_BUILD_ROOT"' EXIT
  XCODE_DERIVED_DATA="$XCODE_BUILD_ROOT/derived-data"
  SWIFT_PACKAGE_CLONES="$XCODE_BUILD_ROOT/package-clones"
  bash swift-clients/tooling/with-build-lock.sh xcodebuild \
    -project swift-clients/apps/macOS/Voucha.xcodeproj -scheme Voucha \
    -disableAutomaticPackageResolution \
    -onlyUsePackageVersionsFromResolvedFile \
    -disablePackageRepositoryCache \
    -clonedSourcePackagesDirPath "$SWIFT_PACKAGE_CLONES" \
    -derivedDataPath "$XCODE_DERIVED_DATA" \
    -destination 'platform=macOS' build \
    SYMROOT="$XCODE_DERIVED_DATA/Build/Products" \
    OBJROOT="$XCODE_DERIVED_DATA/Build/Intermediates.noindex" \
    CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""
)
```

The harness and Xcode wrapper share the per-user policy in
[Host Locks](../docs/development/host-locks.md).

## Architecture

Two shared SwiftPM packages + thin per-platform app shells:

| Package | Contents                                                                                                         |
| ------- | ---------------------------------------------------------------------------------------------------------------- |
| `core/` | `VouchaCore`, `VouchaModels`, `VouchaAPI`, `VouchaAuth`, `VouchaPersistence` — Foundation-only, Android-portable |
| `ui/`   | `VouchaDesignSystem` (reusable components), `VouchaFeatures` (7 tab feature modules)                             |

App shells live under `apps/` and are generated by XcodeGen from `project.yml` files. User-facing
native parity work must be implemented as SwiftUI, not WebView/open-web fallback, unless a PR
documents a concrete blocker and follow-up.

```
swift-clients/
├── core/          Foundation-only SwiftPM package (networking, auth, models, persistence)
├── ui/            SwiftUI SwiftPM package (design system, feature modules)
├── apps/
│   ├── shared-app/   Cross-platform RootView + AppSection routing
│   ├── android/      Skip Fuse shell + ML Kit Prompt API bridge
│   └── macOS/        XcodeGen project.yml + thin app shell
└── tooling/       Bootstrap and code-generation scripts
```

## Coverage

Run with coverage locally:

```sh
bash tooling/with-build-lock.sh swift test --package-path core --force-resolved-versions --enable-code-coverage
bash tooling/with-build-lock.sh swift test --package-path ui --force-resolved-versions --enable-code-coverage
```

To test SwiftUI Views, use [ViewInspector](https://github.com/nalexn/ViewInspector) (already
declared as a test-only dependency in `ui/Package.swift`). Call `try view.inspect()` — no
`Inspectable` conformance needed in 0.10.x. See `ui/Tests/VouchaUITests/DesignSystemViewTests.swift`
for a reference pattern using `EmptyStateView`.

See the [swift-test-authoring skill](../.agents/skills/swift-test-authoring/SKILL.md) for the
required thresholds and exemptions.

CI enforces the 90% patch-coverage threshold with the published `coverage-check` policy engine
against `.coverage-rules.yml` (`pnpm run coverage:swift`, which requires `BASE_SHA` and the CI
LCOV artifacts). To get the same signal locally before pushing, export per-package LCOV and run
the advisory variant from the repository root:

```sh
bash swift-clients/tooling/write-lcov.sh swift-clients/core VouchaCorePackageTests coverage/core/lcov.info
bash swift-clients/tooling/write-lcov.sh swift-clients/ui VouchaUIPackageTests coverage/ui/lcov.info
pnpm run coverage:swift:local
```

`coverage:swift:local` uses the same rules but passes `--advisory` and skips cleanly if
`coverage/{core,ui}/lcov.info` are absent — early insight only, not the gate. Use
`pnpm run test:plan:swift --base origin/main --head HEAD` first to get the affected test commands
to run.
