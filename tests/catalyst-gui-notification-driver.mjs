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
  timezone: ['Africa/Abidjan', 'Africa/Accra', 'America/Los_Angeles', 'UTC'],
}
export function nativeSession(fault = {}, fixture = {}) {
  let clock = 0
  let focused
  let picker
  let pickerGeneration = 0
  const selectedValues = {}
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
      assert.ok(
        [
          '//*[self::XCUIElementTypeSheet or self::XCUIElementTypeAlert][.//XCUIElementTypePickerWheel]',
          '//XCUIElementTypePickerWheel',
        ].includes(body.value),
        'options are not clickable menu elements',
      )
      return {
        value: picker
          ? [
              {
                ELEMENT: `${body.value.startsWith('//*[') ? 'picker-sheet' : 'picker-wheel'}-${pickerGeneration}`,
              },
            ]
          : [],
      }
    }
    if (path === '/elements') {
      return { value: [{ 'element-6066-11e4-a52e-4f735466cecf': body.value }] }
    }
    const match = /^\/element\/([^/]+)\/(.*)$/u.exec(path)
    if (match) {
      const reference = decodeURIComponent(match[1])
      const id = reference.startsWith('picker-') ? reference.replace(/-\d+$/u, '') : reference
      if (
        /^picker-(?:sheet|wheel|done)(?:-copy)?-\d+$/u.test(reference) &&
        (!picker || !reference.endsWith(`-${pickerGeneration}`))
      ) {
        return {
          value: {
            error: 'stale element reference',
            message: 'PickerWheel is not present on the current view',
          },
        }
      }
      const field = id.replace('notification-', '')
      const command = match[2]
      if (command === 'elements' && id === 'picker-sheet') {
        assert.ok(picker, 'action sheet must be open')
        assert.equal(body.using, 'xpath')
        const isWheel = body.value === './/XCUIElementTypePickerWheel'
        assert.ok(isWheel || body.value === './/XCUIElementTypeButton[@label="Done"]')
        if (fault.pickerQueryError === picker.field)
          return { value: { error: 'unknown command', message: 'picker query unavailable' } }
        const missing = isWheel ? fault.noWheel : fault.noDone
        const ambiguous = isWheel ? fault.ambiguousWheel : fault.ambiguousDone
        const id = isWheel ? 'picker-wheel' : 'picker-done'
        return {
          value:
            missing === picker.field
              ? []
              : [id, ...(ambiguous === picker.field ? [id + '-copy'] : [])].map(id => ({
                  ELEMENT: `${id}-${pickerGeneration}`,
                })),
        }
      }
      if (command === 'elements' && id === 'notification-time') {
        assert.equal(body.using, 'xpath')
        assert.equal(body.value, './/XCUIElementTypeTextField')
        if (fault.timeComponentsError)
          return { value: { error: 'unknown command', message: 'component query unavailable' } }
        return {
          value: (fault.noTimeComponents
            ? []
            : ['time-hour', 'time-minute', ...(fault.ambiguousHour ? ['time-hour-copy'] : [])]
          ).map(ELEMENT => ({ ELEMENT })),
        }
      }
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
      if (command === 'attribute/enabled')
        return { value: !(picker && fault.disabledWheel === picker.field && id === 'picker-wheel') }
      if (command === 'attribute/label')
        return {
          value: id.startsWith('time-hour')
            ? 'Hour'
            : id === 'time-minute'
              ? 'Minute'
              : 'Notifications',
        }
      if (command === 'attribute/amType')
        return {
          value:
            id === 'picker-wheel'
              ? fault.wheelType === picker.field
                ? 'XCUIElementTypeTextField'
                : 'XCUIElementTypePickerWheel'
              : id === 'notification-time'
                ? (fault.timeType ?? 'XCUIElementTypeDatePicker')
                : (fault.hourType ?? 'XCUIElementTypeTextField'),
        }
      if (command === 'attribute/value') {
        if (fault.emptyWheel === field) return { value: '' }
        if (id === 'picker-wheel') {
          assert.ok(picker)
          if (!fault.allowWheelValue) return { value: String(picker.index) }
          const value = picker.values[picker.index]
          return {
            value:
              fault.emptyWheel === picker.field
                ? ''
                : picker.field === 'timezone'
                  ? value
                  : `Localized ${value}`,
          }
        }
        const [hour, minute] = state.moderation_email_time_of_day.split(':')
        return {
          value: id.startsWith('time-hour')
            ? String(Number(hour) % 12 || 12)
            : id === 'time-minute'
              ? minute
              : id === 'notification-time'
                ? `${Number(hour) % 12 || 12}:${minute} ${Number(hour) < 12 ? 'AM' : 'PM'}`
                : Object.hasOwn(options, field) && field !== 'timezone'
                  ? fault.emptyWheel === field
                    ? ''
                    : `Localized ${selectedValues[field] ?? state[keys[field]]}`
                  : (selectedValues[field] ?? state[keys[field]]),
        }
      }
      if (command === 'click') {
        if (id === 'picker-done') {
          assert.ok(picker, 'Done requires an open action sheet')
          if (fault.doneError === picker.field)
            return { value: { error: 'invalid element state', message: 'Done unavailable' } }
          const selected = picker.values[picker.index]
          selectedValues[picker.field] = selected
          if (fault.noPickerPatch !== picker.field) persist(keys[picker.field], selected)
          if (fault.noDismissal !== picker.field) picker = undefined
          return { value: null }
        }
        if (Object.hasOwn(options, field)) {
          assert.equal(picker, undefined, 'previous selection must be completed')
          const values =
            field === 'timezone'
              ? [
                  state[keys[field]],
                  ...options.timezone.filter(value => value !== state[keys[field]]),
                ]
              : options[field]
          pickerGeneration += 1
          picker = { field, values, index: values.indexOf(state[keys[field]]) }
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
      if (body.script === 'macos: scroll') {
        assert.ok(picker, 'wheel only exists while its action sheet is open')
        assert.equal(args.elementId, `picker-wheel-${pickerGeneration}`)
        assert.equal(args.deltaX, 0)
        assert.ok([-32, 32].includes(args.deltaY))
        if (fault.wheelKeysError === picker.field)
          return { value: { error: 'invalid element state', message: 'wheel unavailable' } }
        if (fault.stuckWheel !== picker.field) picker.index += args.deltaY < 0 ? 1 : -1
        assert.ok(picker.index >= 0 && picker.index < picker.values.length)
        pickerGeneration += 1
        if (fault.earlyPickerPatch === picker.field)
          persist(keys[picker.field], picker.values[picker.index])
        if (fault.autoCommit === picker.field) {
          if (fault.noPickerPatch !== picker.field)
            persist(keys[picker.field], picker.values[picker.index])
          picker = undefined
        }
        return { value: null }
      } else if (body.script === 'macos: keys') {
        assert.equal(args.keys.at(-1), 'XCUIKeyboardKeyReturn')
        if (focused === 'time-hour') {
          assert.equal(args.elementId, 'time-hour')
          assert.equal(args.keys.length, 2)
          assert.ok(['XCUIKeyboardKeyUpArrow', 'XCUIKeyboardKeyDownArrow'].includes(args.keys[0]))
          if (fault.timeKeysError)
            return { value: { error: 'invalid element state', message: 'hour is not editable' } }
          const [hour, minute] = state.moderation_email_time_of_day.split(':')
          if (!fault.noTimePatch)
            persist(
              'moderation_email_time_of_day',
              `${String(Number(hour) + (args.keys[0] === 'XCUIKeyboardKeyUpArrow' ? 1 : -1)).padStart(2, '0')}:${minute}`,
            )
        } else assert.fail('native Picker cannot be edited as a text field or menu')
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
