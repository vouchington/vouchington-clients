# App-shell and notification parity evidence

[Issue #91](https://github.com/vouchington/vouchington-clients/issues/91) remains open until its
child acceptance lanes and producer evidence are complete. A compiled app, linked view test,
fixture-backed UI inspection, or passing subset of controls does not establish full parity.

## Required hosted lanes

The `Native contract tests` workflow's `Tests` fan-in requires the real macOS, iPhone, iPad, and
Mac Catalyst shell jobs to succeed. A missing GUI prerequisite or skipped selected test fails its
runtime lane. The runner and fixture contracts are described in
[native CI test placement](native-ci-test-placement.md).

| Lane              | Current evidence                                                                     | Remaining acceptance owner                                                                                                               |
| ----------------- | ------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Swift macOS       | Root shell, email OTP sign-in, selected video/podcast/posts navigation               | [#80](https://github.com/vouchington/vouchington-clients/issues/80), [#77](https://github.com/vouchington/vouchington-clients/issues/77) |
| Swift iPhone      | Selected root/feed/sign-in simulator executions                                      | [#78](https://github.com/vouchington/vouchington-clients/issues/78), #77                                                                 |
| Swift iPad        | Root-shell simulator execution                                                       | #78, #77                                                                                                                                 |
| .NET Mac Catalyst | Running production shell through a Debug fixture seam; partial notification controls | [#60](https://github.com/vouchington/vouchington-clients/issues/60), #78                                                                 |

These lanes provide hosted runtime evidence. They do not establish physical-device behavior,
signed production installation, store delivery, or notification permission/push delivery on a device.

## Catalyst partial-evidence gate

The Catalyst GUI session records a named check only after its real Appium assertion succeeds:
heading exposure, an engagement switch PATCH, moderation schedule visibility with hidden-value
preservation, and canonical-route focus/announcement dispatch. Schema 3 identifies this as partial
hosted switch evidence and requires `manualAcceptanceRequired: true`. Picker, time, weekday, and
VoiceOver interaction acceptance remains a [#60 manual blocker](catalyst-notification-acceptance.md).
Totals are derived from the named checks rather than a handwritten success count.

CI validates the exact current check set, successful outcomes, evidence schema, platform and scope,
and the verified native revision and pinned producer commit. Missing, duplicate, unexpected,
skipped, failed, malformed, stale or cross-contract evidence fails the gate. A matching count with
different check names is insufficient. Fixture and transport failures still fail the session; a
validator cannot turn a failed Appium run into acceptance. The result and existing failure
diagnostics stay in the Catalyst job's bounded diagnostic artifact.

This gate protects the evidence already executed; it does not implement the missing acceptance
scenarios. The producer pin is evidence identity, not proof that all producer behavior has been
exercised. No check may be renamed as full notification parity merely because this validator passes.

## Graduation requirements

Before advancing a parity matrix or closing #91, require green executable child lanes for:

- All eight controls: engagement, news digest, moderation, community digest, cadence, days, time,
  and timezone.
- Conditional moderation schedule visibility and preservation of hidden schedule values and timezone.
- One-field persisted mutations, pending-submit behavior, failure reconciliation, and a reload that
  proves persistence.
- Canonical notification-route activation through navigation or a deep link, heading focus, and
  exactly one localized announcement for each activation.
- Required non-skipping launches and the agreed flows on each declared shell, with fixture and
  producer contract evidence tied to the same candidate revision.

#60 owns the complete Catalyst notification interaction and accessibility acceptance. #78 owns
shared launch/fixture contracts, #80 owns macOS runtime flows, and #77 owns broader end-to-end flow
coverage. Add each completed scenario to the executable lane and its evidence contract together;
passing source assertions or manually changing this document is insufficient.

Device-only notification delivery, accessibility behavior on actual hardware, signing identities,
provisioning, signed-release installation, and store acceptance require owner/device access. Record
those separately when available, with artifact/build and target identity; do not relabel hosted
fixtures or unsigned builds as that evidence.
