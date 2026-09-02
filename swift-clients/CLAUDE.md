# swift-clients

Native Swift client apps for Voucha — macOS first, then iOS/iPad, then Android via Swift. See
[README.md](README.md) for prerequisites, generation, build/test commands, configuration, coverage,
formatting, the code-quality harness, and known gotchas.

Native parity work must stay native. Do not use WebView, embedded web, or open-web fallback for
user-facing features unless the accepted plan documents a concrete blocker, the PR explains it, and
a follow-up issue tracks replacing it with SwiftUI.

Before adding or changing a Swift test, load the
[swift-test-authoring skill](../.agents/skills/swift-test-authoring/SKILL.md) — it owns coverage
thresholds/exemptions, the ViewInspector pattern, the test file-length cap, and the `URLProtocol`
test-double synchronization rules.

Use [README.md#code-quality-harness](README.md#code-quality-harness) for formatting, lint, build,
test, and generation checks. Use the harness or CI wrappers for compiler-heavy commands; see
[Per-User Host Locks](../docs/development/host-locks.md).

## Key rules

- **`core/` is Foundation-only** — no AppKit, UIKit, SwiftUI, SwiftData, or Network. POSIX libc (`getaddrinfo`, `connect`) is allowed for portable local-LLM DNS pinning. **Machine-enforced by `ast-grep-rules/swift-core-foundation-only.yml`** (runs in the Linux static-analysis CI job on every PR). `AuthenticationServices` is allowed only inside `VouchaAuth` for WebAuthn/passkey ceremony types, enforced by `ast-grep-rules/swift-authenticationservices-in-auth-only.yml`; presentation anchors must still be supplied by UI/app layers. `APIClient.responseLines(for:)` on Linux/Android must keep SSE/NDJSON streaming incremental via delegate callbacks while buffering non-2xx bodies long enough for `validate(response:data:)`.
- **CI placement** — Linux owns portable core and `test-support`. macOS still runs the full core suite so Darwin `URLSession`, Security/Keychain, core LCOV, and Swift DTO parity are exercised. Dual-OS is required because those jobs compile different stacks, not for confidence. Gate Darwin-only tests with `#if canImport(Darwin)` or `#if canImport(Security)`. See [native CI test placement](../docs/development/native-ci-test-placement.md).
- **`VouchaPersistence` uses a `CacheStore` protocol** — no SwiftData anywhere in `core/` even as a dependency. SwiftData belongs in a platform-specific store behind this protocol.
- **XcodeGen, not committed `.xcodeproj`** — `apps/*/project.yml` is the source of truth. `DerivedData/` and `*.xcodeproj/` are gitignored.
- **Native UI copy uses `VouchaLocalization`** — app-owned presentation text is a typed `UiMessageKey`/`UiMessage`. Use `UiVerbatimText.verbatim` for dynamic user/server values; literal external-provider, protocol, and user-content boundaries must use the explicit `externalProvider`, `protocolValue`, or `userContent` constructors. The `swift-no-hardcoded-view-copy`, `swift-no-hardcoded-presentation-data`, and `swift-no-direct-presentation-formatting` AST-grep rules reject raw SwiftUI/helper copy, raw presentation state and metadata, enum-label transforms, and locale-bypassing formatting across UI and app sources. Generated localization sources are excluded from SwiftFormat and SwiftLint because `pnpm run native-localization:check` is their canonical drift gate.
- **Manifest claims follow real Swift consumers** — every typed key used under `apps/**` or
  `ui/Sources/**` must carry a `swift` claim in
  `ts-shared/ui-messages/native-consumer-manifest.mts`, and every `swift` claim must retain a real
  product reference. Do not reference generated `.__plural.*`/`.__select.*` variants directly;
  `UiMessages` selects them from the canonical descriptor leaf. Run
  `pnpm run native-localization:generate && pnpm run native-localization:check` after changing a
  key, claim, or product reference.
- **Frozen Swift dependencies** — each package's `Package.resolved` is canonical. Normal build/test commands use `--force-resolved-versions`; update dependencies explicitly with `swift package --package-path swift-clients/<core|ui|apps/android> update` and commit the resulting lock. Android Skip updates must also keep `apps/android/tooling/materialize-skip-sdk.sh` version and checksum fields in sync.
- **File length** — Swift source uses `.swiftlint.yml` (`200/250` warning/error); the test cap lives in the [swift-test-authoring skill](../.agents/skills/swift-test-authoring/SKILL.md).
- **Auth uses HTTP cookies over URLSession** — `KeychainCookieStorage` persists `dt` (device) and `st` (session) cookies. The backend sets/clears them via `Set-Cookie` headers.
- **`OpenAICompatibleResponsesClient` must send every request through `LocalLLMConnectionPinning`** — resolve, keep only private or local IPs, rewrite the URL to that IP, then `URLSession`. Do not call `session.data` from the client. Do not add a skip-pin flag. Do not import `Network` for this path.
- **API core parity** — endpoint route/fixture changes must stay aligned with `web/lib/api/client/**`, `api-fixtures/v1`, and `dotnet-clients/src/Voucha.Client.Core/Api/**`.
- **Cursor pagination is table stakes** — every database-backed list forwards opaque cursors and appends pages safely; follow Filaments' [cross-surface pagination contract](https://github.com/jonathanong/filaments/blob/main/docs/overview/architecture/pagination.md).
- **Sandbox-compatible entitlements** — `apps/macOS/Voucha.entitlements` enables App Sandbox + Keychain access group; `ViewModelFactory` must pass that group to cookie and App Attest key stores.

## See Also

- [README.md](README.md) — commands, harness, gotchas
- [Native CI test placement](../docs/development/native-ci-test-placement.md)
- Native client strategy: Filaments' [native-clients.md](https://github.com/jonathanong/filaments/blob/main/docs/overview/architecture/native-clients.md)
