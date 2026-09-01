# .NET Deep Linking

The .NET client handles deep links with the native MAUI shell, not a browser or WebView fallback.

## Flow

1. Windows and Mac Catalyst register the `voucha` custom protocol.
2. MAUI platform hooks capture protocol activations.
3. A thin app-layer dispatcher buffers URLs until `AppShell` is ready.
4. `AppShell` resolves the URL and selects an existing shell target or opens Session for login
   links.

This keeps route selection in the shell, where the app owns the tab and flyout tree.

## Code path

- [`AppLinkDispatcher`](https://github.com/vouchington/vouchington-clients/blob/main/dotnet-clients/src/Voucha.Client.App/AppLinkDispatcher.cs)
  buffers URLs until the shell attaches its handler.
- [`NativeDeepLinkResolver`](https://github.com/vouchington/vouchington-clients/blob/main/dotnet-clients/src/Voucha.Client.Core/Navigation/NativeDeepLinkResolver.cs)
  accepts `voucha://...` and relative paths, then maps known native routes to shell intents.
- [`AppShell`](https://github.com/vouchington/vouchington-clients/blob/main/dotnet-clients/src/Voucha.Client.App/AppShell.cs)
  owns routeable shell items, handles auth-gated handoff, and selects the existing `ShellContent`
  route.
- The Windows app manifest and Mac Catalyst `Info.plist` register the `voucha` scheme.

## Route behavior

- Login links open Session and may prefill `emailAddress` and `otp`; redemption always waits for
  an explicit Verify tap, so an external link cannot silently swap the local session.
- Authenticated routes remain pending while signed out and resume after authentication. Public
  profile links resolve only to their typed native scope; unknown subpaths and unsupported filters
  are ignored instead of opening an unrelated history view.
- Topic/source import and export share a native destination. The route supplies an export filter,
  while imports remain unrestricted by that filter.
- Post and comment links open native post detail. Direct-message links switch conversation and
  clear the compose editor just as manual selection does.
- Apple Sign-In on Windows returns through the web callback route to
  `voucha://auth/apple/callback` before shared sign-in continues.
- `voucha-turnstile://...` is an in-process bridge for the reusable MAUI Turnstile challenge. Its
  `Navigating` handler intercepts it before OS routing, so it is intentionally not an app manifest
  protocol registration.
- Route matching preserves percent-encoded path and query components until typed bindings decode
  them once. Path parsing does not treat `+` as a space; query parsing does.

The resolver chooses an intent; `AppShell` owns navigation state. Transport, validation, and source
import polling stay in `Voucha.Client.Core`, while file selection, temporary export files, and the
OS share sheet stay in the MAUI layer.

## Related guidance

- [Native client current footprint](reference-native-clients-current-footprint.md)
- [Filaments native client strategy](https://github.com/jonathanong/filaments/blob/main/docs/overview/architecture/native-clients.md)
- [Filaments client parity matrix](https://github.com/jonathanong/filaments/blob/main/docs/requirements/CLIENT-PARITY-MATRIX.md)
