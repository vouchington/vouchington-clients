import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { isAbsolute, join } from 'node:path'

export const notificationLocalizationFile =
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessages.resx'
const announcementKey = 'native.dotnet.settings.notificationSettingsOpened'
const fields = [
  'is_engagement_emails_enabled',
  'news_digest_frequency',
  'is_moderation_emails_enabled',
  'community_digest_frequency',
  'moderation_email_cadence',
  'moderation_email_days_of_week',
  'moderation_email_time_of_day',
  'moderation_email_timezone',
]

export function notificationInputs(root) {
  assert.ok(typeof root === 'string' && isAbsolute(root), 'VOUCHA_CONTRACT_ROOT must be absolute')
  const base = join(root, 'api-fixtures/v1')
  const manifest = JSON.parse(readFileSync(join(base, 'manifest.json'), 'utf8'))
  const fixture = manifest.fixtures?.find(item => item.id === 'native.my.email-preferences.default')
  assert.ok(
    fixture?.method === 'GET' &&
      fixture.path === '/api/v1/my/email-preferences' &&
      fixture.status === 200,
    'canonical notification fixture is required',
  )
  assert.ok(
    typeof fixture.bodyFile === 'string' &&
      fixture.bodyFile.startsWith('responses/') &&
      !fixture.bodyFile.includes('..'),
    'invalid notification fixture path',
  )
  const { email_preferences: preferences } = JSON.parse(
    readFileSync(join(base, fixture.bodyFile), 'utf8'),
  )
  keys(preferences, fields, 'canonical preferences')
  const xml = readFileSync(join(root, notificationLocalizationFile), 'utf8')
  const matches = [
    ...xml.matchAll(
      /<data\s+name="native\.dotnet\.settings\.notificationSettingsOpened"[^>]*>\s*<value>([^<]*)<\/value>\s*<\/data>/gu,
    ),
  ]
  assert.equal(matches.length, 1, `generated ${announcementKey} is required`)
  const entities = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" }
  const announcement = matches[0][1].replace(/&([^;]+);/gu, (_, entity) => {
    if (Object.hasOwn(entities, entity)) return entities[entity]
    if (/^#(?:[0-9]+|x[0-9a-f]+)$/iu.test(entity))
      return String.fromCodePoint(
        entity[1] === 'x' ? Number.parseInt(entity.slice(2), 16) : Number(entity.slice(1)),
      )
    throw new Error('unsupported generated localization entity')
  })
  assert.ok(announcement.length > 0, 'empty generated notification announcement')
  return { preferences, announcement }
}

// This validates app dispatches; it does not claim VoiceOver delivery.
export function validateNotificationAppEvidence(report, { preferences, announcement }) {
  keys(
    report,
    ['schemaVersion', 'initialPreferences', 'patches', 'activations', 'accessibilityDispatches'],
    'app evidence',
  )
  assert.equal(report.schemaVersion, 1, 'unsupported app evidence schema')
  assert.deepEqual(
    report.initialPreferences,
    preferences,
    'app evidence must start from the canonical fixture',
  )
  assert.ok(Array.isArray(report.patches), 'app evidence patches must be an array')
  const covered = new Set()
  let state = preferences
  for (const patch of report.patches) {
    keys(patch, ['fields', 'before', 'after'], 'persisted PATCH')
    assert.ok(
      Array.isArray(patch.fields) && patch.fields.length === 1 && fields.includes(patch.fields[0]),
      'PATCH must contain one known preference field',
    )
    const field = patch.fields[0]
    assert.deepEqual(patch.before, state, 'PATCH history must be contiguous')
    keys(patch.after, fields, 'persisted preferences')
    assert.notDeepEqual(patch.after[field], state[field], 'PATCH must persist a changed value')
    assert.deepEqual(
      patch.after,
      { ...state, [field]: patch.after[field] },
      'PATCH changed hidden or unrelated values',
    )
    covered.add(field)
    state = patch.after
  }
  assert.equal(covered.size, fields.length, 'all eight controls must persist a mutation')
  assert.ok(
    Array.isArray(report.activations) && report.activations.length === 2,
    'two canonical route activations are required',
  )
  assert.ok(
    Array.isArray(report.accessibilityDispatches),
    'accessibility dispatches must be an array',
  )
  for (const [index, activation] of report.activations.entries()) {
    keys(activation, ['activationId', 'canonicalPath'], 'route activation')
    assert.equal(activation.activationId, index + 1, 'activation IDs must be distinct and ordered')
    assert.equal(activation.canonicalPath, '/my/notification-settings')
    const events = report.accessibilityDispatches.filter(
      item => item?.activationId === activation.activationId,
    )
    const focus = events.filter(item => item.kind === 'focus' && item.succeeded === true)
    const announcements = events.filter(item => item.kind === 'announcement')
    assert.equal(
      focus.length,
      1,
      'one successful heading focus dispatch is required per activation',
    )
    assert.equal(focus[0].automationId, 'notification-preferences-heading')
    assert.equal(announcements.length, 1, 'one announcement dispatch is required per activation')
    assert.equal(
      announcements[0].text,
      announcement,
      'announcement must match generated localization',
    )
    assert.ok(
      events.indexOf(focus[0]) < events.indexOf(announcements[0]),
      'heading focus must precede announcement',
    )
  }
  for (const event of report.accessibilityDispatches) {
    assert.ok(event && [1, 2].includes(event.activationId), 'orphan accessibility dispatch')
    if (event.kind === 'focus') {
      keys(event, ['kind', 'activationId', 'automationId', 'succeeded'], 'focus dispatch')
      assert.equal(typeof event.succeeded, 'boolean')
      assert.equal(event.automationId, 'notification-preferences-heading')
    } else {
      keys(event, ['kind', 'activationId', 'text'], 'announcement dispatch')
      assert.equal(event.kind, 'announcement', 'unknown accessibility dispatch')
    }
  }
}

function keys(value, expected, description) {
  assert.ok(value && typeof value === 'object' && !Array.isArray(value), `invalid ${description}`)
  assert.deepEqual(Object.keys(value).sort(), [...expected].sort(), `invalid ${description} fields`)
}
