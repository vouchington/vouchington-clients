# Catalyst notification acceptance

## Required hosted evidence

The required Debug Mac2 session exercises the engagement and moderation switches with native clicks.
Each click must persist exactly one PATCH containing its API field, with contiguous before/after
snapshots of all eight preferences. The moderation schedule must appear, disappear, and reappear;
the six preferences not edited by the hosted session must remain unchanged, including hidden values.
The session starts with the pinned identity and preference fixtures and never sends a fixture-only
mutation command.

The session leaves notification settings through the preferences deep link, waits for the app audit to confirm that the non-notification route was applied, then reopens `/my/notification-settings` in the same application. Each activation must
dispatch successful heading focus followed by exactly one announcement matching the generated
localization key `native.dotnet.settings.notificationSettingsOpened`. Repeated route applications
within one route epoch do not activate it again; stale loads cannot activate a departed route.

The Debug-only fixture handler writes `result.json.app.json` beside the Mac2 result. CI independently
validates both files against the native revision and pinned producer inputs before uploading them.
Previous reports are removed before launch. Schema 3 explicitly identifies
`issue-60-hosted-switches-and-accessibility-dispatch` and requires `manualAcceptanceRequired: true`.
It contains four hosted checks, not passed or skipped claims for the manual controls. Missing switch
PATCHes, unrelated mutations, duplicate announcements, and deadline overruns fail the existing lane.
The session and three-minute workflow step budgets are unchanged.

## Manual blockers for #60

A green hosted preflight is partial evidence and does not graduate notification-settings parity.
Keep #60 and the parity matrix open until a human records the following on the actual Catalyst app:

- Change both digest pickers and cadence through the native wheel and Done action. Confirm the
  selected value and the exact single-field persisted PATCH for each action.
- Select `selected_days`, verify the day controls appear, edit a day, and confirm persistence and
  preservation of other schedule values. Verify the day controls hide for other cadences.
- Edit the native time control and select a different system timezone, including an offscreen wheel
  value. Complete native confirmation and verify exactly the intended persisted field changes.
- Observe VoiceOver heading focus and the localized spoken announcement on initial activation and
  after leaving and reopening the canonical route.

Record this manual evidence separately for #60; do not label it a hosted schema-3 success. Core tests
cover all eight view-model mutation payloads. App tests cover rendering, events, synchronization,
fixture persistence, and dispatch hooks. Neither these tests nor protocol doubles prove the manual
native interactions or VoiceOver delivery.

## Why sheets are outside hosted preflight

On hosted macOS, even a native Done predicate took 30.4 seconds and a subsequent `displayed` call
repeatedly re-snapshotted the open sheet for more than 30 seconds, exhausting the three-minute step
in run [43181942430](https://github.com/vouchington/vouchington-clients/actions/runs/43181942430).
Earlier XPath and stale-sheet lookups also stalled. Direct clicking or keyboard dismissal would
still depend on unverified native sheet behavior. The hosted session therefore never opens picker,
time, or day editors and never queries sheet elements. Manual blockers remain explicit instead of
raising timeouts or reporting synthetic passes.

For local validation, run `pnpm run contracts:pull`, export its printed `VOUCHA_CONTRACT_ROOT`, run
`pnpm run quality:check`, and use the native changed-file planners. Run App.Tests in Debug as well as
Release when validating the fixture audit. Native acceptance uses the pinned Xcode toolchain;
retain page-source diagnostics for failed manual interactions. The app report proves semantic focus
and announcement dispatch, not assistive-technology delivery.

## Phase and departure budgets

Run [43185497327](https://github.com/vouchington/vouchington-clients/actions/runs/43185497327)
finished switch work in about 21 seconds, then exhausted the ten-second departure poll while Mac2
continued reporting the retained shared-Settings heading as present. This was not exhaustion of
the 140-second session budget: native state queries typically took 0.33–0.77 seconds.

The app audit uses schema 2 with `notificationRouteActive`. Actual non-notification route
application records `false`; a subsequent canonical activation records `true`. Departure polls
that file instead of taking repeated native snapshots. Return still requires a new canonical
activation, its focus/announcement dispatches, and an exposed heading. Notification failures
retain the audit report without requesting another page-source snapshot; the independent root
shell session keeps its own diagnostics.

Readiness, mutation/visibility work, departure, and return have caps of 75, 45, 10, and 10 seconds
respectively, all bounded by the existing shared 140-second deadline. Each request uses the smallest
remaining phase, poll, and session budget. A read that exhausts its poll budget fails before another
sleep; nonpositive remaining time is never passed to a timer. The final observation and evidence
read must also complete within the return and shared budgets.

## Cached Settings routing regression

Run [43188316323](https://github.com/vouchington/vouchington-clients/actions/runs/43188316323)
accepted the outbound URL, but its audit retained one notification activation and an active route.
The uploaded diagnostics contained the three switch PATCHes and startup navigation confirmation,
with no screenshots or hierarchy capture. An accepted OS URL command alone does not establish that
the destination was applied.

The route-application defect was in AppShell's cached-page enumeration. MAUI 10.0.20 stores a
`ContentTemplate`-created page in its shell content cache, exposed by `IShellContentController.Page`;
`ShellContent.Content` remains null. Reading `Content` missed the displayed Settings page, so a new
match was queued for creation while navigation reused the existing page without applying the match.
Startup preflight had masked this by applying its initial notification match directly.

Enumeration now reads the instantiated controller page without creating an unvisited template.
The App regression exercises the real MAUI template cache, production settings-route helper, and
production notification focus method; it verifies that the ordinary Settings route hides the
notification section and records audit departure. The GUI still sends the real outbound URL and
requires the departure audit before returning; no synthetic route or audit mutation is substituted.
