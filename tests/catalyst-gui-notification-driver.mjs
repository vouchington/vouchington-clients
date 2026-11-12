import assert from 'node:assert/strict'
import { assertNotificationControls } from '../dotnet-clients/tooling/catalyst-gui-notification-session.mjs'
import { inputs } from './catalyst-notification-app-fixture.mjs'

const keys = {
  engagement: 'is_engagement_emails_enabled',
  moderation: 'is_moderation_emails_enabled',
}
export function nativeSession(fault = {}, fixture = {}) {
  let clock = 0
  let sectionGeneration = 1
  let controlSequence = 0
  const controls = new Map()
  let pendingReads = fault.initialDelayReads ?? 0
  let restoreRoute = pendingReads > 0
  let departed = false
  const calls = []
  const initialPreferences = structuredClone(fixture.preferences ?? inputs.preferences)
  const announcement = fixture.announcement ?? 'Notification settings opened'
  let state = structuredClone(initialPreferences)
  const evidence = {
    schemaVersion: 3,
    notificationRouteActive: !restoreRoute,
    controls: {
      engagement: initialPreferences.is_engagement_emails_enabled,
      moderation: initialPreferences.is_moderation_emails_enabled,
      scheduleVisible: initialPreferences.is_moderation_emails_enabled,
      ready: pendingReads === 0,
    },
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
  function applyControls() {
    evidence.controls = {
      engagement: state.is_engagement_emails_enabled,
      moderation: state.is_moderation_emails_enabled,
      scheduleVisible: Boolean(fault.hiddenScheduleVisible || state.is_moderation_emails_enabled),
      ready: !fault.eventsBlocked,
    }
  }
  async function readEvidence() {
    if (pendingReads > 0 && --pendingReads === 0) {
      if (restoreRoute) {
        evidence.notificationRouteActive = true
        restoreRoute = false
      }
      applyControls()
    }
    return structuredClone(evidence)
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
    evidence.controls.ready = false
    pendingReads = fault.uiDelayReads ?? 0
    if (pendingReads === 0) applyControls()
  }
  const request = async (method, url, body) => {
    calls.push({ method, url, body })
    assert.ok(url.startsWith('/session/native/'))
    const path = url.slice('/session/native'.length)
    if (fault.stale && path.endsWith('/click')) throw new Error('stale element reference')
    if (path === '/element') {
      assert.equal(method, 'POST')
      assert.equal(body.using, 'accessibility id')
      assert.equal(
        body.value,
        'notification-preferences-section',
        'only the section may use an app-wide lookup',
      )
      assert.equal(
        evidence.notificationRouteActive,
        true,
        'wait for the applied route before any lookup',
      )
      assert.equal(evidence.controls.ready, true, 'no native snapshots during transitions')
      if (fault.missingOnce && calls.filter(call => call.url.endsWith('/element')).length === 1)
        return { value: { error: 'no such element', message: 'section loading' } }
      return { value: { ELEMENT: `notification-preferences-section-${sectionGeneration}` } }
    }
    const match = /^\/element\/([^/]+)\/(.*)$/u.exec(path)
    if (match) {
      const id = decodeURIComponent(match[1])
      const command = match[2]
      assert.equal(evidence.controls.ready, true, 'no native snapshots during transitions')
      if (command === 'element') {
        assert.equal(
          id,
          `notification-preferences-section-${sectionGeneration}`,
          'refresh scope after route transitions',
        )
        assert.equal(body.using, 'accessibility id')
        assert.ok(
          [
            'notification-preferences-heading',
            'notification-moderation-schedule',
            'notification-engagement',
            'notification-moderation',
          ].includes(body.value),
          'manual controls must not be opened',
        )
        if (body.value === 'notification-moderation-schedule')
          assert.equal(evidence.controls.scheduleVisible, true)
        const reference = `${body.value}-${++controlSequence}`
        controls.set(reference, body.value.replace('notification-', ''))
        return { value: { ELEMENT: reference } }
      }
      assert.equal(command, 'click', 'post-action attributes/displayed snapshots are forbidden')
      const field = controls.get(id)
      assert.ok(
        field && Object.hasOwn(keys, field),
        'never reuse a descendant reference across mutation/layout changes',
      )
      controls.clear()
      persist(keys[field], !state[keys[field]])
      return { value: null }
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
          evidence.controls.ready = false
          departed = true
          evidence.notificationRouteActive = false
        }
        return { value: null }
      }
      assert.equal(args.url, 'voucha://my/notification-settings')
      assert.equal(departed, true, 'same focused route cannot create another activation')
      sectionGeneration += 1
      pendingReads = fault.returnDelayReads ?? 0
      evidence.controls.ready = false
      if (pendingReads === 0) applyControls()
      evidence.notificationRouteActive = true
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
    readEvidence,
    run: overrides =>
      assertNotificationControls(request, 'native', {
        readEvidence,
        now: () => clock,
        sleep: async ms => {
          clock += ms
        },
        expectedAnnouncement: announcement,
        ...overrides,
      }),
  }
}
