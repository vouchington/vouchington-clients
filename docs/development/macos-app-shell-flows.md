# macOS app-shell flows

Issue [#80](https://github.com/vouchington/vouchington-clients/issues/80) requires actual macOS
app interaction, beyond compilation or view inspection. The hosted `Swift macOS app smoke` job
selects these six XCUITest cases explicitly and requires six passed tests, zero failures and zero
skips. It retains the WindowServer and GUI-console ownership preflight, deterministic backend
transport, existing ad-hoc launch entitlements, and failure diagnostics.

| Flow            | Required runtime assertion                                                   |
| --------------- | ---------------------------------------------------------------------------- |
| Root shell      | Root marker exists and the sign-in action is hittable, building on #78/#618  |
| Email OTP       | Enter email and code through the sign-in sheet; signed-out action disappears |
| Videos          | Canonical video title appears, disappears when leaving for News, and returns |
| Podcasts        | Canonical episode appears, disappears when leaving for News, and returns     |
| Posts           | Canonical post appears, disappears when leaving for News, and returns        |
| Videos recovery | First eligible video request fails locally; Retry recovers the canonical row |

The recovery scenario is an explicit Debug-only launch input to the existing isolated transport.
It injects a local connection failure once, rather than publishing a synthetic backend response.
Retry must use the real feed error action and load the unchanged
`swift.rss-feed-items.feed.default` payload. A source check or mocked transport test cannot count
as execution of this app-shell case. Protocol tests establish deterministic request matching,
one-shot failure and cancellation behavior separately.

The email scenario proves the sign-in seam; feed scenarios restore the canonical identity through
the shared fixture setup. They are separate app launches and do not claim a single live-backend
sign-in-to-feed journey.

## Remaining acceptance

News rendering remains blocked on a producer-owned Swift article fixture. The current pin has no
Swift fixture for `GET /api/v1/feeds/rss_feed_items/any` with `limit=20` and
`media_type=article`. The producer must publish a fixture declared for Swift with that request,
a successful feed envelope containing an actual article row and a nonempty title, and the
associated schema/fixture export. Existing canonical identity can be reused. The client pin must
then adopt the merged producer commit before adding News rendering/navigation coverage.

`web.rss-feed-items.feed.default` is unsuitable: it declares other consumers, uses `limit=25`,
and contains video content. Replaying it as Swift News would not establish the missing behavior.

Candidate-specific hosted execution of all six selected tests is required. Local package or
hostless protocol tests are supporting evidence only. Physical-device, store-installation and
signed-release acceptance remain separate; these deterministic hosted macOS flows do not certify
them. Local signing or automation permission failures must be reported without weakening the
runtime assertions or adding skip passes.
