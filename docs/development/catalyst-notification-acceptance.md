# Catalyst notification acceptance

The required Debug Mac2 session operates the eight notification controls through native clicks,
keyboard gestures, and menu selection. It starts with the pinned identity and preference fixtures.
Each gesture must produce exactly one PATCH containing exactly its API field, with a contiguous
persisted before/after snapshot. Schedule and day visibility are checked as moderation and cadence
change; the snapshots also check preservation of hidden values.

The session leaves notification settings through the preferences deep link, waits for the heading
to disappear, then reopens `/my/notification-settings` in the same application. Each activation must
dispatch successful heading focus followed by exactly one announcement matching the generated
localization key `native.dotnet.settings.notificationSettingsOpened`. Repeated route applications
within one route epoch do not activate it again; stale loads cannot activate a departed route.

The Debug-only fixture handler writes `result.json.app.json` beside the Mac2 result. CI independently
validates both files against the native revision and pinned producer inputs before uploading them.
Previous reports are removed before launch. Missing reports, unsupported native controls, incorrect
mutations, duplicate announcements, and required GUI prerequisites fail the existing lane.

This evidence records the application's semantic focus and announcement **dispatches**. It does not
establish that VoiceOver delivered focus or spoke the announcement. Keep the parity matrix unchanged
until the complete hosted acceptance, including the required accessibility observation, is green.

For local validation, run `pnpm run contracts:pull`, export its printed `VOUCHA_CONTRACT_ROOT`, run
`pnpm run quality:check`, and use the native changed-file planners. Run App.Tests in Debug as well as
Release so the fixture audit is exercised. Node protocol tests cover the Mac2 command path and
failures; they do not substitute for the hosted running-app session. If a native time control exposes
a different type, retain the failed session's page-source diagnostics and implement its actual
native interaction before accepting the lane. Do not substitute a fixture-only mutation command.
