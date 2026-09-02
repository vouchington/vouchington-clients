# dotnet-clients

.NET client apps for Voucha. Windows desktop is the product target; Mac Catalyst is the parity and
CI testing client, and Android is outside this workspace. Use [README.md](README.md) for current
status, structure, SDK/configuration details, and local commands.
Batch all compatible changed .NET files in one `pnpm run test:plan:dotnet` invocation before
running selected targets.
Use the harness or CI wrappers for compiler-capable commands; see
[Per-User Host Locks](../docs/development/host-locks.md).
Before adding or changing a .NET test, load the
[dotnet-test-authoring skill](../.agents/skills/dotnet-test-authoring/SKILL.md) — it owns the
Core-vs-App test split, the test file-length cap, and local coverage.

## Rules

- Put network, persistence, auth/session, and view-model behavior in `Voucha.Client.Core` unless it requires a platform API.
- `Voucha.Client.Core` must not reference MAUI or platform namespaces. This is machine-enforced by `ast-grep-rules/cs-core-no-platform-types.yml`.
- `Voucha.Client.App` must not pass `async` lambdas to void `Action` APIs (`Command`, `BeginInvokeOnMainThread`). Wrap as `() => _ = FooAsync()`. This is machine-enforced by no-mistakes `csharp-no-async-void-delegate`. App Mac Catalyst builds also run Meziantou `MA0147` / `MA0134` / `MA0100` / `MA0182` as errors; App keeps `AnalysisMode=None` so SDK CA does not flood MAUI.
- Native release defaults must not point at localhost. This is machine-enforced by `ast-grep-rules/cs-no-localhost-native-defaults.yml`.
- `OpenAICompatibleResponsesClient` must only be constructed with a handler from its `CreatePinnedHandler()` factory, never a hand-rolled `SocketsHttpHandler` — the factory is what wires the connect-time DNS-rebinding guard (`LocalLLMConnectionPinning`) that re-validates a local-model endpoint's actually-resolved peer address before a cleartext connection is allowed. This is machine-enforced by `ast-grep-rules/cs-no-raw-local-llm-handler.yml`.
- Keep `src/Voucha.Client.Core/Api/**` endpoint helpers and DTOs synchronized with `web/lib/api/client/**`, `swift-clients/core`, and shared `api-fixtures/v1`.
- Native TLS pinning follows Filaments' [native TLS pinning runbook](https://github.com/jonathanong/filaments/blob/main/docs/runbooks/native-tls-pinning.md): pin Cloudflare edge SPKIs only, and skip localhost/custom dev origins.
- Keep MAUI pages thin; bind to core view models or adapters.
- C# presentation state must carry generated `UiMessageKey` or `UiText` values and use
  `IUiLocalization` formatting. Raw user/server values and structural protocol values must cross an
  explicit `UiText.Verbatim`, `UiInvariantText`, or similarly typed boundary.
- MAUI presentation copy in XAML must use generated `{DynamicResource ...}` keys. Use
  the locale-versioned `{app:UiLocalizedValue ...}` multi-binding for localized interpolation and
  locale-aware date/number formatting so already-visible values refresh after a locale change;
  every referenced key and `{value}` message placeholder is checked against generated RESX.
  Literal values are reserved for the guard's explicit structural glyph, protocol, and input-data
  cases.
- Every `UiMessageKey` used by C# and every localization key referenced by MAUI XAML must carry a
  `dotnet` claim in `ts-shared/ui-messages/native-consumer-manifest.mts`; every claim must retain a
  real product consumer. Do not list or directly reference generated
  `.__plural.*`/`.__select.*` variants as independent keys. Run
  `pnpm run native-localization:generate && pnpm run native-localization:check` after changing a
  key, claim, or product reference.
- Store .NET native session cookies through `SessionCookieJar` and the app-layer MAUI
  `SecureStorage` adapter. `ISessionStore` is the release-facing session contract; raw `dt`/`st`
  development cookie loading must stay in `#if DEBUG` code only.
- Keep posts, referral links, and topics native in MAUI; do not use WebView fallback for parity surfaces.
- Keep member chat and support native in MAUI, with list/detail/create flows backed by core chat/support services.
- Every database-backed native list must forward opaque cursors and append pages safely; follow Filaments' [cross-surface pagination contract](https://github.com/jonathanong/filaments/blob/main/docs/overview/architecture/pagination.md).
- Do not add Windows CI packaging steps until a Windows self-hosted runner exists.
- **CI placement** — portable Core tests, DTO parity, and patch coverage run once on Linux. Do not add a macOS copy of `Voucha.DotNet.sln`. MAUI App tests and Mac Catalyst smoke stay macOS. See [native CI test placement](../docs/development/native-ci-test-placement.md).
- Keep source files under the native cap: 200 physical lines for `dotnet-clients/src/**/*.cs`,
  enforced by `repo-file-policy`. The test cap and Core-vs-App split live in the
  [dotnet-test-authoring skill](../.agents/skills/dotnet-test-authoring/SKILL.md).

## See Also

- [README.md](README.md) — status, structure, commands
- [Native CI test placement](../docs/development/native-ci-test-placement.md)
- Native client strategy: Filaments' [native-clients.md](https://github.com/jonathanong/filaments/blob/main/docs/overview/architecture/native-clients.md)
- [.NET deep-linking architecture](../docs/overview/architecture/dotnet-deep-linking.md)
