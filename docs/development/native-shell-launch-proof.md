# Native app-shell launch proof

Issue [#78](https://github.com/vouchington/vouchington-clients/issues/78) concerns a running app
and a visible root surface. Compiling the app, constructing a page in a unit test, or finding an
invisible accessibility marker alone does not establish that result.

## Hosted baseline inventory

The latest completed native main run inspected for this change is
[43135963900](https://github.com/vouchington/vouchington-clients/actions/runs/43135963900),
at `4eff14b6e44392a5ab9ca715de78babe2ff8652a`. Its logs contain these executed results;
they are baseline evidence, not execution of this pull request's new assertions.

| Target           | Hosted execution                                                                                                       | Baseline result                                                                                                           | Root proof added by this change                                                                                        |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| macOS            | [Swift macOS app smoke](https://github.com/vouchington/vouchington-clients/actions/runs/43135963900/job/130911792575)  | Root case passed; all five launch/feature cases passed with zero failures and skips.                                      | The shared root case also requires the existing sign-in control to be hittable.                                        |
| iPhone Simulator | [Root-shell shard](https://github.com/vouchington/vouchington-clients/actions/runs/43135963900/job/130914004699)       | One root case passed, zero failures and skips.                                                                            | The same shared hittable-control assertion runs on the iPhone target.                                                  |
| iPad Simulator   | [iPad root-shell launch](https://github.com/vouchington/vouchington-clients/actions/runs/43135963900/job/130914004614) | One root case passed, zero failures and skips.                                                                            | The same shared hittable-control assertion runs on the iPad target.                                                    |
| Mac Catalyst     | [GUI preflight](https://github.com/vouchington/vouchington-clients/actions/runs/43135963900/job/130911792578)          | Three notification control checks passed through the actual app and Appium Mac2. Initial root rendering was not asserted. | A separate root session requires three real root controls to be displayed before the independent notification session. |

The separate `.NET MAUI Mac Catalyst` job builds the app. Its success is build evidence only.
The iPhone feed and email-sign-in shards are additional interaction evidence, not substitutes for
the dedicated root-shell shard. The baseline Swift root case checked a mounted accessibility
marker; this change retains that assertion and adds a visible, hittable control.

## Required launch contracts

- macOS requires WindowServer and ownership of the GUI console, then runs the actual Xcode app
  UI-test target. The result gate requires seven passed tests, zero failures, and zero skips, including the
  [Videos error/Retry recovery case](macos-app-shell-flows.md).
- iPhone and iPad use actual Simulator app launches followed by the Xcode UI-test target. Each
  dedicated root execution must report exactly one passed case, zero failures, and zero skips.
  Failure diagnostics and `.xcresult` artifacts remain available.
- Catalyst retains its GUI console preflight and Debug fixture transport. `--root-session` starts
  the production DI-created `AppShell` without the notification-navigation override. Mac2 must
  find the session heading, email input, and send-code action displayed; the action must also be
  enabled. A separately launched `--session` retains the notification interaction checks owned by
  [#60](https://github.com/vouchington/vouchington-clients/issues/60).
- Catalyst writes `root-result.json` and `result.json` separately. The root result must contain
  three passed runtime control checks, zero failed checks, and zero skips. The notification result
  retains the [partial-evidence gate](native-shell-notification-evidence.md#catalyst-partial-evidence-gate):
  four named successful checks, the exact evidence schema and scope, and matching native and pinned
  producer revisions. These are Mac2 assertions, not XCTest case counts or full notification parity.
  Any launch, visibility, or result-validation failure fails the required job; a build or mocked
  protocol test cannot produce a native launch pass.
- Appium requests a fresh app launch and termination on session deletion, so the root argument
  cannot be lost by attaching to a process left by the notification session.

The existing deterministic fixture transport remains distinct from live-backend acceptance. No
physical device, store installation, developer certificate, or signed production-release result
is claimed by these lanes. The macOS smoke uses its existing ad-hoc signing configuration.

## Evidence boundaries

The shared Swift assertion is compiled into the macOS, iPhone, and iPad launch targets. Portable
launch-option tests, rendered MAUI tests, workflow contract tests, and mocked Mac2 protocol tests
cover their respective code paths, but do not count as native launch results. Capture the candidate
revision's hosted results before declaring all four targets accepted.

Local Catalyst runtime verification requires the Xcode version selected by the pinned MAUI
workload. A different installed Xcode does not certify that target. GUI ownership, automation
permissions, and signing failures must remain explicit failures; do not grant permissions, invent
skip passes, or treat Simulator evidence as physical-device acceptance.

Changes to fixture launch modes should preserve the notification checks in #60, macOS sign-in and
navigation coverage in [#80](https://github.com/vouchington/vouchington-clients/issues/80), and
shared fixture/harness boundaries in [#91](https://github.com/vouchington/vouchington-clients/issues/91).
