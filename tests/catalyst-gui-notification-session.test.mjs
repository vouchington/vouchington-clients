import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import { nativeSession } from './catalyst-gui-notification-driver.mjs'

describe('notification Mac2 native gestures and persisted acceptance', () => {
  it('drives every control through native gestures and audits each exact PATCH', async () => {
    const session = nativeSession()
    const checks = await session.run()
    assert.deepEqual(
      checks.map(check => check.name),
      [
        'heading-exposed',
        'engagement-mutation-persisted',
        'news-digest-mutation-persisted',
        'community-digest-mutation-persisted',
        'moderation-visibility-and-hidden-preservation',
        'cadence-mutation-persisted',
        'days-mutation-persisted',
        'time-mutation-persisted',
        'timezone-mutation-persisted',
        'canonical-route-focus-and-announcement-dispatched',
      ],
    )
    assert.ok(checks.every(check => check.status === 'passed'))
    assert.equal(session.evidence.patches.length, 9)
    assert.ok(session.calls.some(call => call.url.endsWith('/notification-day-2/click')))
    assert.ok(session.calls.some(call => call.body?.script === 'macos: deepLink'))
  })
  for (const [fault, message] of [
    [{ stale: true }, /stale element reference/u],
    [{ unsupported: true }, /Mac2 unknown command/u],
    [{ wrongField: true }, /exactly its API field/u],
    [{ corruptUnrelated: true }, /changed an unrelated field/u],
    [{ hiddenScheduleVisible: true }, /schedule visibility differs/u],
    [{ hiddenDaysVisible: true }, /schedule visibility differs/u],
    [{ noDeparture: true }, /departure from notification settings did not settle/u],
    [{ duplicate: true }, /multiple PATCH requests/u],
    [{ noPatch: true }, /engagement persisted PATCH did not settle/u],
    [{ timeType: 'XCUIElementTypeDatePicker' }, /unsupported native time control/u],
    [{ focusFailure: true }, /Expected values to be strictly equal/u],
    [{ wrongAnnouncement: true }, /generated localization/u],
    [{ duplicateAnnouncement: true }, /exactly one announcement/u],
  ]) {
    it(`fails closed for ${JSON.stringify(fault)}`, async () => {
      await assert.rejects(nativeSession(fault).run(), message)
    })
  }
  it('retries only a transient missing native element during initial load', async () => {
    const session = nativeSession({ missingOnce: true })
    assert.equal((await session.run()).length, 10)
    assert.ok(
      session.calls
        .slice(0, 2)
        .every(
          call =>
            call.url === '/session/native/element' &&
            call.body?.value === 'notification-preferences-heading',
        ),
    )
  })
  it('requires a persisted report instead of accepting clicks alone', async () => {
    await assert.rejects(
      nativeSession().run({ readEvidence: undefined }),
      /readEvidence is required/u,
    )
  })
  it('uses one session budget instead of adding startup waits for every control', async () => {
    await assert.rejects(nativeSession({ noPatch: true }).run({ timeoutMs: 500 }), /deadline/u)
  })
  it('rejects an unknown report schema before sending any mutation gesture', async () => {
    const session = nativeSession()
    await assert.rejects(
      session.run({ readEvidence: async () => ({ schemaVersion: 2 }) }),
      /unsupported notification app report/u,
    )
    assert.equal(
      session.calls.some(call => call.url.endsWith('/click')),
      false,
    )
  })
})
