import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { assertNotificationControls } from '../dotnet-clients/tooling/catalyst-gui-notification-session.mjs'

const keys = {
  engagement: 'is_engagement_emails_enabled',
  moderation: 'is_moderation_emails_enabled',
  'news-digest': 'news_digest_frequency',
  'community-digest': 'community_digest_frequency',
  cadence: 'moderation_email_cadence',
  timezone: 'moderation_email_timezone',
}
const options = {
  'news-digest': ['none', 'daily', 'weekly'],
  'community-digest': ['none', 'daily', 'weekly'],
  cadence: ['daily', 'selected_days', 'weekly'],
  timezone: ['UTC', 'America/Los_Angeles'],
}
function nativeSession(fault = {}) {
  let clock = 0
  let focused
  let notificationRouteVisible = true
  let departed = false
  const calls = []
  const initialPreferences = {
    is_engagement_emails_enabled: true,
    is_moderation_emails_enabled: true,
    news_digest_frequency: 'weekly',
    community_digest_frequency: 'none',
    moderation_email_cadence: 'daily',
    moderation_email_days_of_week: [1, 3],
    moderation_email_time_of_day: '08:00',
    moderation_email_timezone: 'UTC',
  }
  let state = structuredClone(initialPreferences)
  const evidence = {
    schemaVersion: 1,
    initialPreferences,
    patches: [],
    activations: [{ activationId: 1, canonicalPath: '/my/notification-settings' }],
    accessibilityDispatches: [
      {
        activationId: 1,
        kind: 'focus',
        automationId: 'notification-preferences-heading',
        succeeded: true,
      },
      { activationId: 1, kind: 'announcement', text: 'Notification settings opened' },
    ],
  }
  function persist(field, value) {
    if (fault.noPatch) return
    const before = structuredClone(state)
    state = { ...state, [field]: value }
    evidence.patches.push({
      fields: fault.wrongField ? ['unrelated'] : [field],
      before,
      after: structuredClone(state),
    })
    if (fault.corruptUnrelated) evidence.patches.at(-1).after.news_digest_frequency = 'none'
    if (fault.duplicate) evidence.patches.push(structuredClone(evidence.patches.at(-1)))
  }
  const request = async (method, url, body) => {
    calls.push({ method, url, body })
    assert.ok(url.startsWith('/session/native/'))
    const path = url.slice('/session/native'.length)
    if (fault.stale && path.endsWith('/click')) throw new Error('stale element reference')
    if (path === '/element') {
      if (fault.missingOnce && calls.length === 1)
        return { value: { error: 'no such element', message: 'heading loading' } }
      assert.equal(method, 'POST')
      assert.equal(body.using, 'accessibility id')
      return { value: { 'element-6066-11e4-a52e-4f735466cecf': body.value } }
    }
    if (path === '/elements' && body.using === 'xpath') {
      assert.equal(body.value, '//*[@label="America/Los_Angeles"]')
      return { value: [{ ELEMENT: 'timezone-option' }] }
    }
    if (path === '/elements') {
      return { value: [{ 'element-6066-11e4-a52e-4f735466cecf': body.value }] }
    }
    const match = /^\/element\/([^/]+)\/(.*)$/u.exec(path)
    if (match) {
      const id = decodeURIComponent(match[1])
      const field = id.replace('notification-', '')
      const command = match[2]
      if (command === 'displayed') {
        if (field === 'preferences-heading') return { value: notificationRouteVisible }
        if (field.startsWith('day-'))
          return {
            value:
              fault.hiddenDaysVisible ||
              (state.is_moderation_emails_enabled &&
                state.moderation_email_cadence === 'selected_days'),
          }
        return {
          value:
            !['cadence', 'time', 'timezone'].includes(field) ||
            fault.hiddenScheduleVisible ||
            state.is_moderation_emails_enabled,
        }
      }
      if (command === 'attribute/enabled') return { value: true }
      if (command === 'attribute/label') return { value: 'Notifications' }
      if (command === 'attribute/amType')
        return { value: fault.timeType ?? 'XCUIElementTypeTextField' }
      if (command === 'attribute/value') return { value: state[keys[field]] }
      if (command === 'click') {
        if (id === 'timezone-option') {
          persist('moderation_email_timezone', 'America/Los_Angeles')
          return { value: null }
        }
        focused = field
        if (['engagement', 'moderation'].includes(field)) persist(keys[field], !state[keys[field]])
        else if (field.startsWith('day-')) {
          const day = Number(field.slice(4))
          const days = state.moderation_email_days_of_week
          persist(
            'moderation_email_days_of_week',
            days.includes(day)
              ? days.filter(value => value !== day)
              : [...days, day].sort((a, b) => a - b),
          )
        }
        return { value: null }
      }
    }
    if (path === '/execute/sync') {
      if (fault.unsupported)
        return { value: { error: 'unknown command', message: 'keys unavailable' } }
      const args = body.args[0]
      if (body.script === 'macos: keys') {
        assert.equal(args.keys.at(-1), 'XCUIKeyboardKeyReturn')
        if (focused === 'time') {
          assert.deepEqual(args.keys[0], { key: 'a', modifierFlags: 16 })
          assert.equal(args.elementId, 'notification-time')
          persist('moderation_email_time_of_day', args.keys[1])
        } else {
          const values = options[focused]
          let index = values.indexOf(state[keys[focused]])
          for (const key of args.keys.slice(0, -1)) {
            assert.ok(['XCUIKeyboardKeyDownArrow', 'XCUIKeyboardKeyUpArrow'].includes(key))
            index += key === 'XCUIKeyboardKeyDownArrow' ? 1 : -1
          }
          assert.ok(index >= 0 && index < values.length)
          persist(keys[focused], values[index])
        }
      } else if (body.script === 'macos: deepLink') {
        if (args.url === 'voucha://my/preferences') {
          if (!fault.noDeparture) {
            notificationRouteVisible = false
            departed = true
          }
          return { value: null }
        }
        assert.equal(args.url, 'voucha://my/notification-settings')
        assert.equal(departed, true, 'same focused route cannot create another activation')
        notificationRouteVisible = true
        evidence.activations.push({ activationId: 2, canonicalPath: '/my/notification-settings' })
        evidence.accessibilityDispatches.push(
          {
            activationId: 2,
            kind: 'focus',
            automationId: 'notification-preferences-heading',
            succeeded: !fault.focusFailure,
          },
          {
            activationId: 2,
            kind: 'announcement',
            text: fault.wrongAnnouncement ? 'Unrelated' : 'Notification settings opened',
          },
        )
        if (fault.duplicateAnnouncement)
          evidence.accessibilityDispatches.push({
            activationId: 2,
            kind: 'announcement',
            text: 'Notifications',
          })
      } else assert.fail(`unexpected native command ${body.script}`)
      return { value: null }
    }
    assert.fail(`unexpected protocol request ${method} ${path}`)
  }
  return {
    calls,
    evidence,
    run: overrides =>
      assertNotificationControls(request, 'native', {
        readEvidence: async () => structuredClone(evidence),
        now: () => clock,
        sleep: async ms => {
          clock += ms
        },
        expectedAnnouncement: 'Notification settings opened',
        ...overrides,
      }),
  }
}

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
