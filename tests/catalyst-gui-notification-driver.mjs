import assert from 'node:assert/strict'
import { assertNotificationControls } from '../dotnet-clients/tooling/catalyst-gui-notification-session.mjs'
import { inputs } from './catalyst-notification-app-fixture.mjs'

const keys = {
  engagement: 'is_engagement_emails_enabled',
  moderation: 'is_moderation_emails_enabled',
}
export function nativeSession(fault = {}, fixture = {}) {
  let clock = 0
  let notificationRouteVisible = true
  let departed = false
  const calls = []
  const initialPreferences = structuredClone(fixture.preferences ?? inputs.preferences)
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
      assert.ok(
        [
          'notification-preferences-heading',
          'notification-engagement',
          'notification-moderation',
        ].includes(body.value),
        'manual controls must not be opened',
      )
      return { value: { ELEMENT: body.value } }
    }
    if (path === '/elements') {
      assert.equal(
        body.using,
        'accessibility id',
        'hosted preflight must never query native sheets',
      )
      assert.ok(
        [
          'notification-preferences-heading',
          'notification-cadence',
          'notification-time',
          'notification-timezone',
        ].includes(body.value),
      )
      return { value: [{ ELEMENT: body.value }] }
    }
    const match = /^\/element\/([^/]+)\/(.*)$/u.exec(path)
    if (match) {
      const id = decodeURIComponent(match[1])
      const field = id.replace('notification-', '')
      const command = match[2]
      if (command === 'displayed')
        return {
          value:
            field === 'preferences-heading'
              ? notificationRouteVisible
              : !['cadence', 'time', 'timezone'].includes(field) ||
                fault.hiddenScheduleVisible ||
                state.is_moderation_emails_enabled,
        }
      assert.ok(
        Object.hasOwn(keys, field),
        'no element-state calls on manual picker/time/day controls',
      )
      if (command === 'attribute/enabled') return { value: true }
      if (command === 'attribute/value') return { value: state[keys[field]] }
      if (command === 'click') {
        persist(keys[field], !state[keys[field]])
        return { value: null }
      }
    }
    if (path === '/execute/sync') {
      if (fault.unsupported)
        return { value: { error: 'unknown command', message: 'deepLink unavailable' } }
      assert.equal(
        body.script,
        'macos: deepLink',
        'hosted preflight must never send wheel, keyboard, or fixture mutation commands',
      )
      const args = body.args[0]
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
