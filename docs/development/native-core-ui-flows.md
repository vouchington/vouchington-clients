# Native sign-in and News UI flows

This is partial coverage of [#77](https://github.com/vouchington/vouchington-clients/issues/77),
separate from root launch proof in [#78](https://github.com/vouchington/vouchington-clients/issues/78).
The macOS app-shell work remains with [#80](https://github.com/vouchington/vouchington-clients/issues/80).
Compiling a test, exercising a protocol double, and running a native UI scenario are different results.

## Required CI slices

| Slice                 | Execution                                                                                                                                                                                    | Required result                                                                      |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| iPhone/iPad email OTP | Existing `SignInFlowTests` drives the signed-out control, email entry, send-code, OTP entry, and verification.                                                                               | One passed test, zero failures and skips.                                            |
| iPhone/iPad News      | `NewsFlowTests` starts an isolated News-only fixture session, requires the canonical primary row, then expands and collapses related articles.                                               | One passed test, zero failures and skips.                                            |
| iOS fixture transport | Hostless `VouchaFlowTransportTests` tests article routing, malformed input rejection, and rejection of video/audio/other endpoints in News mode.                                             | Three passed tests, zero failures and skips; supporting transport evidence only.     |
| MAUI Mac Catalyst     | A fresh Mac2 session drives email OTP, requires authenticated identity, opens the production `voucha://feed/news` route in the exact candidate app, then expands and collapses related rows. | Six named successful runtime assertions with matching native and producer revisions. |

The iPhone and iPad slices consume the existing unsigned, run/attempt/revision/Xcode/SDK-bound test-products
artifact. Its manifest also records the unique transport-test bundle; both consumers validate its
path and existence. Root, video, sign-in, and News checks remain independent. The iPad matrix preserves its root-launch
check and adds separate sign-in and News shards using the same build artifact.

Catalyst preserves the independent root and notification sessions from #618. The new session uses a
separate fixture directory, result file, and Appium port. It requests a fresh app and terminates its
session before writing success evidence. The required workflow validates
`scripts/catalyst-auth-news-evidence.mjs` before uploading `auth-news-result.json`. Aggregate counts,
missing checks, skipped checks, stale revisions, or another evidence scope cannot satisfy the gate.

## Backend and fixture boundaries

Both sign-in flows use explicit Debug-only test-backend seams. They validate fixed disposable email,
Turnstile-token, and OTP inputs; they do not deliver real email or complete a live CAPTCHA. The
Catalyst session remains anonymous until the strict OTP exchange succeeds, then returns the canonical
identity through the ordinary authentication and session pipeline. Its in-memory session storage and
test transport activate only for the explicit flow launch mode. Unexpected requests never reach a
live backend. The legacy OTP handshake is app-owned test protocol behavior, matching the existing
Swift seam, rather than a claim that the producer's `/auth/native/*` fixtures cover legacy routes.

News replays the unchanged `web.rss-feed-items.feed.default` response from the pinned producer.
The manifest declares an article request with limit 25; the native clients request limit 20. The
fixture's items also retain video media metadata despite its article query and feed classification.
The News-only transports require the actual native article request and reject video/audio fallback.
This proves News routing, rendering, and related-row interaction against that replay, not producer
article-filter or pagination-limit parity. No shared producer fixture is edited or relabeled.

## Flake budget and evidence

The automatic test-retry budget is **zero**. Every shard and Mac2 scenario must pass on its first
execution, with zero skipped cases. Existing job and step deadlines remain unchanged; the new
Catalyst scenario has its own three-minute step inside the fifteen-minute job. One monotonic
150-second deadline caps Appium readiness, Mac2 startup, every HTTP request, navigation, and UI polling.
A separate ten-second budget covers diagnostics and teardown, leaving twenty seconds for fixture
staging, ad-hoc signing, and step overhead. Mac2 startup retains a 100-second maximum and is shortened
when needed to preserve HTTP response overhead. Text entry has a 30-second request cap for Mac2
accessibility lookup and input synthesis; other ordinary requests retain ten seconds. Both caps
consume the same remaining deadline, and text entry is never retried. Accessibility source is
requested only after failure, with its two-second cap inside the cleanup budget. Local process
readiness waits are bounded. Network-download retries do not rerun tests or turn failures into passes.
Failures retain existing Simulator summaries/logs or the Catalyst native accessibility tree and fail
the required job. Manually rerunning a candidate is a new execution, not evidence that the first passed.

Unsigned Simulator build-for-testing and Debug linked-source tests are available without developer
certificates. Actual execution still requires a compatible Simulator service, or the pinned Catalyst
Xcode/MAUI toolchain and an owned GUI session. Missing tools, signing rejection, or automation
permissions must be reported as unexecuted failures; do not alter signing, TCC, or skip handling to
manufacture a pass. Protocol tests can validate the driver's assertions but cannot produce native
runtime acceptance.

## Remaining acceptance

- macOS sign-in and app-shell interactions: #80 owns that scope.
- Actual Apple provider UI, live OTP delivery/CAPTCHA, physical devices, and store-installed builds:
  these are outside the isolated fixture flows and need their normal accounts, configuration, or signing.
- Producer fixture alignment: native-limit article fixtures with consistent media metadata are needed
  before claiming endpoint/data parity from the News replay.
- Candidate-specific hosted native results: authoring or compiling these scenarios does not establish
  their successful execution. Keep #77 open until its platform acceptance is evidenced.
