import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import { assertNotificationControls } from '../dotnet-clients/tooling/catalyst-gui-notification-session.mjs'
import { nativeSession } from './catalyst-gui-notification-driver.mjs'
import { validateNotificationAppEvidence } from '../scripts/catalyst-notification-app-evidence.mjs'

describe('notification Mac2 native gestures and persisted acceptance', () => {
  it('drives the hosted switches and audits each exact PATCH', async () => {
    const session = nativeSession()
    const checks = await session.run()
    assert.deepEqual(
      checks.map(check => check.name),
      [
        'heading-exposed',
        'engagement-mutation-persisted',
        'moderation-visibility-and-hidden-preservation',
        'canonical-route-focus-and-announcement-dispatched',
      ],
    )
    assert.ok(checks.every(check => check.status === 'passed'))
    assert.equal(session.evidence.patches.length, 3)
    assert.ok(session.calls.some(call => call.body?.script === 'macos: deepLink'))
  })
  it('supports a saved disabled moderation switch without losing any manual values', async () => {
    const preferences = nativeSession().evidence.initialPreferences
    preferences.is_moderation_emails_enabled = false
    const session = nativeSession({}, { preferences })
    await session.run()
    validateNotificationAppEvidence(session.evidence, {
      preferences,
      announcement: 'Notification settings opened',
    })
    assert.equal(session.evidence.patches.length, 4)
  })
  it('does not open manual controls or query picker-sheet elements', async () => {
    const session = nativeSession()
    await session.run()
    assert.ok(session.calls.length < 70, 'hosted command count remains bounded')
    assert.ok(
      session.calls
        .filter(call => call.url.endsWith('/click'))
        .every(call => /notification-(?:engagement|moderation)\/click$/u.test(call.url)),
    )
    assert.ok(
      session.calls
        .filter(call => call.body?.script)
        .every(call => call.body.script === 'macos: deepLink'),
    )
    assert.ok(
      session.calls
        .filter(call => call.body?.using)
        .every(call => call.body.using === 'accessibility id'),
    )
  })
  for (const [fault, message] of [
    [{ stale: true }, /stale element reference/u],
    [{ unsupported: true }, /Mac2 unknown command/u],
    [{ wrongField: true }, /exactly its API field/u],
    [{ corruptUnrelated: true }, /changed an unrelated field/u],
    [{ hiddenScheduleVisible: true }, /schedule visibility differs/u],
    [{ noDeparture: true }, /departure from notification settings did not settle/u],
    [{ duplicate: true }, /multiple PATCH requests/u],
    [{ noPatch: true }, /engagement persisted PATCH did not settle/u],
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
    assert.equal((await session.run()).length, 4)
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
      session.run({ readEvidence: async () => ({ schemaVersion: 1 }) }),
      /unsupported notification app report/u,
    )
    assert.equal(
      session.calls.some(call => call.url.endsWith('/click')),
      false,
    )
  })
})

// A completed HTTP response cannot turn an expired session into passing evidence.
it('cancels in-flight native requests and rejects a response that arrives past the budget', async () => {
  const session = nativeSession()
  let clock = 0
  let signal
  await assert.rejects(
    assertNotificationControls(
      async (...args) => {
        signal = args[3].signal
        const result = await session.request(...args)
        clock = 501
        return result
      },
      'native',
      {
        readEvidence: async () => session.evidence,
        expectedAnnouncement: 'fixture',
        now: () => clock,
        timeoutMs: 500,
      },
    ),
    /deadline/u,
  )
  assert.ok(signal instanceof AbortSignal)
})

it('refuses to absorb any startup PATCH for a fixture with saved preferences', async () => {
  const session = nativeSession()
  const before = structuredClone(session.evidence.initialPreferences)
  session.evidence.patches.push({
    fields: ['is_engagement_emails_enabled'],
    before,
    after: { ...before, is_engagement_emails_enabled: !before.is_engagement_emails_enabled },
  })
  await assert.rejects(session.run(), /load must not mutate saved preferences/u)
  assert.equal(session.calls.filter(call => call.url.endsWith('/click')).length, 0)
})

for (const phase of ['observation', 'final report']) {
  it(`rejects success when the ${phase} crosses the shared deadline`, async () => {
    const session = nativeSession()
    let clock = 0
    let observing = false
    await assert.rejects(
      session.run({
        timeoutMs: 500,
        now: () => clock,
        sleep: async ms => {
          if (ms === 250 && session.evidence.activations.length === 2) {
            observing = true
            if (phase === 'observation') clock = 501
          }
        },
        readEvidence: async () => {
          if (observing && phase === 'final report') clock = 501
          return structuredClone(session.evidence)
        },
      }),
      /shared deadline/u,
    )
    assert.equal(observing, true)
  })
}

it('uses the applied-route audit when the shared Settings page retains its heading', async () => {
  const session = nativeSession({ retainedHeading: true })
  await session.run()
  const departure = session.calls.findIndex(
    call => call.body?.args?.[0]?.url === 'voucha://my/preferences',
  )
  const returning = session.calls.findIndex(
    call => call.body?.args?.[0]?.url === 'voucha://my/notification-settings',
  )
  assert.ok(departure >= 0 && returning > departure)
  assert.equal(
    session.calls.slice(departure + 1, returning).length,
    0,
    'departure uses file audit only, without native snapshots',
  )
})

it('never schedules a negative delay after an evidence read crosses its poll deadline', async () => {
  const session = nativeSession({ noPatch: true })
  let clock = 0
  let reads = 0
  const delays = []
  await assert.rejects(
    session.run({
      now: () => clock,
      readEvidence: async () => {
        if (++reads === 3) clock = 10_045
        return structuredClone(session.evidence)
      },
      sleep: async ms => {
        delays.push(ms)
        clock += ms
      },
    }),
    /engagement persisted PATCH did not settle/u,
  )
  assert.deepEqual(delays, [])
})

for (const [phase, matches, duration] of [
  ['readiness', call => call.url.endsWith('/element'), 75_001],
  ['mutations', call => call.url.endsWith('/notification-engagement/click'), 45_001],
  ['departure', call => call.body?.args?.[0]?.url === 'voucha://my/preferences', 10_001],
  ['return', call => call.body?.args?.[0]?.url === 'voucha://my/notification-settings', 10_001],
]) {
  it(`enforces the ${phase} budget before the shared deadline`, async () => {
    const session = nativeSession()
    let clock = 0
    await assert.rejects(
      assertNotificationControls(
        async (...args) => {
          const response = await session.request(...args)
          if (matches(session.calls.at(-1))) clock = duration
          return response
        },
        'native',
        {
          now: () => clock,
          sleep: async ms => {
            assert.ok(ms > 0)
            clock += ms
          },
          readEvidence: async () => structuredClone(session.evidence),
          expectedAnnouncement: 'Notification settings opened',
        },
      ),
      new RegExp(`${phase} phase exceeded its deadline`, 'u'),
    )
  })
}
