# Native Client Current Footprint

This page records the client-owned implementation footprint. Product parity requirements and the
cross-surface API contract remain owned by the [Vouchington native client strategy](https://github.com/vouchington/vouchington/blob/main/docs/overview/architecture/native-clients.md)
and [client parity matrix](https://github.com/vouchington/vouchington/blob/main/docs/requirements/CLIENT-PARITY-MATRIX.md).

## Swift

- `swift-clients/core` is the Foundation-only portable package for API transport, authentication,
  persistence contracts, pagination state, and local-model connection policy.
- `swift-clients/ui` contains SwiftUI presentation and generated localization resources.
- `swift-clients/apps` contains platform heads. Xcode projects are generated from the checked-in
  `project.yml` files; generated projects and build products are not committed.
- Apple clients use App Attest for supported CAPTCHA-gated actions and the native Turnstile
  challenge only for unsupported devices or failed attestation. Android uses the Swift shell and
  the same portable core rather than a WebView fallback.

## .NET

- [`dotnet-clients/Voucha.DotNet.sln`](https://github.com/vouchington/vouchington-clients/tree/main/dotnet-clients)
  owns the portable .NET client CI.
- The MAUI app targets `net10.0-maccatalyst` and `net10.0-windows10.0.26100.0`. Mac Catalyst is
  the current smoke-build proxy; Windows is the product target and is built locally until a
  supported Windows runner is available.
- `Voucha.Client.Core` owns typed API services, authentication, persistence/session contracts,
  pagination, and view models. `Voucha.Client.App` owns the thin MAUI shell and platform adapters.
- Posts, referral links, sources, topics, friends, notifications, chat, support, and settings
  surfaces are native MAUI views backed by portable C# services. Native routes must not fall back
  to a browser or WebView.

## Shared native behavior

- Both clients forward opaque `after` cursors, append pages by stable entity id, preserve rows on
  continuation failure, and reset traversal when filters change. The API and parity requirements
  are defined by Vouchington's [pagination contract](https://github.com/vouchington/vouchington/blob/main/docs/overview/architecture/pagination.md).
- Story rows retain their first displayed primary and prefetched related articles from
  `story_member_pages`. Expanding uses the existing preview without a request; explicit load more
  appends up to 25 members through the scoped story endpoint while preserving rows on delay,
  failure, and retry. The localized count shows the number loaded with `+` until traversal is
  exhausted, then uses the exact singular or plural. Repeated stories on later feed pages retain
  the displayed preview and cursor. Shared deliveries remain standalone and use the existing
  item-id deduplication. This contract is coordinated through
  [Vouchington #990](https://github.com/vouchington/vouchington/issues/990).
- Native chat supports OS-managed language models and local OpenAI-compatible Responses API
  endpoints. Endpoint profiles, bearer credentials, and selected provider ids remain device-local;
  local generation is text-only and never silently falls back to another provider.
- Session cookies are stored through platform-secure stores and keyed by API origin. Native OAuth,
  passkey, and deep-link callbacks remain inside their platform app boundaries.
- Direct media playback is native where a direct URL is available. Embed-only media keeps its
  explicit external-source action and does not launch a browser as an implicit playback route.
- Subscription screens are status and plan presentation only; purchase, cancellation, billing,
  and receipt validation remain outside this client repository.

## Ownership boundaries

Vouchington produces the shared API fixtures and localization contract consumed through the explicit
`VOUCHA_FILAMENTS_CONTRACT_ROOT`. It also owns cross-surface route, pagination, TLS, and parity
decisions. This repository owns the Swift/.NET implementation, native test harnesses, and the
client-specific deep-linking behavior documented in [`.NET deep linking`](dotnet-deep-linking.md).
