import assert from 'node:assert/strict'
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
export function nativeSession(fault = {}, fixture = {}) {
  let clock = 0
  let focused
  let notificationRouteVisible = true
  let departed = false
  const calls = []
  const initialPreferences = structuredClone(
    fixture.preferences ?? {
      is_engagement_emails_enabled: true,
      is_moderation_emails_enabled: true,
      news_digest_frequency: 'weekly',
      community_digest_frequency: 'none',
      moderation_email_cadence: 'daily',
      moderation_email_days_of_week: [1, 3],
      moderation_email_time_of_day: '08:00',
      moderation_email_timezone: 'UTC',
    },
  )
  const announcement = fixture.announcement ?? 'Notification settings opened'
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
      { activationId: 1, kind: 'announcement', text: announcement },
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
      assert.equal(
        body.value,
        `//*[@label="${state.moderation_email_timezone === 'UTC' ? 'America/Los_Angeles' : 'UTC'}"]`,
      )
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
          persist(
            'moderation_email_timezone',
            state.moderation_email_timezone === 'UTC' ? 'America/Los_Angeles' : 'UTC',
          )
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
            text: fault.wrongAnnouncement ? 'Unrelated' : announcement,
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
    request,
    run: overrides =>
      assertNotificationControls(request, 'native', {
        readEvidence: async () => structuredClone(evidence),
        now: () => clock,
        sleep: async ms => {
          clock += ms
        },
        expectedAnnouncement: announcement,
        ...overrides,
      }),
  }
}
