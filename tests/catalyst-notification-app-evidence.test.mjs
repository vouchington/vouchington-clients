import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import {
  notificationInputs,
  validateNotificationAppEvidence,
} from '../scripts/catalyst-notification-app-evidence.mjs'
import { appReport, inputs, writeInputs } from './catalyst-notification-app-fixture.mjs'

it('validates contiguous one-field persisted mutations and two localized dispatch activations', () => {
  const report = appReport()
  report.accessibilityDispatches.unshift({
    kind: 'focus',
    activationId: 1,
    automationId: 'notification-preferences-heading',
    succeeded: false,
  })
  validateNotificationAppEvidence(report, inputs)
})

const corruptions = {
  'missing report': () => null,
  'unknown report field': report => {
    report.completeParity = true
  },
  'wrong schema': report => {
    report.schemaVersion = 2
  },
  'noncanonical initial fixture': report => {
    report.initialPreferences = {}
  },
  'missing patches': report => {
    report.patches = null
  },
  'missing hosted switch mutation': report => {
    report.patches.pop()
  },
  'multiple patch fields': report => {
    report.patches[0].fields.push('news_digest_frequency')
  },
  'manual control mutation claimed as hosted': report => {
    report.patches[0].fields = ['news_digest_frequency']
    report.patches[0].after = { ...report.initialPreferences, news_digest_frequency: 'daily' }
  },
  'unknown patch field': report => {
    report.patches[0].fields = ['unknown']
  },
  'stale snapshot': report => {
    report.patches[1].before = report.initialPreferences
  },
  'hidden value lost': report => {
    report.patches[0].after.moderation_email_time_of_day = '12:00'
  },
  'unchanged persisted value': report => {
    report.patches[0].after = report.patches[0].before
  },
  'malformed snapshot': report => {
    report.patches[0].after = []
  },
  'missing route reactivation': report => {
    report.activations.pop()
  },
  'duplicate activation': report => {
    report.activations[1].activationId = 1
  },
  'wrong route': report => {
    report.activations[1].canonicalPath = '/my/preferences'
  },
  'missing dispatches': report => {
    report.accessibilityDispatches = null
  },
  'failed focus': report => {
    report.accessibilityDispatches[0].succeeded = false
  },
  'wrong heading': report => {
    report.accessibilityDispatches[0].automationId = 'other-heading'
  },
  'duplicate announcement': report => {
    report.accessibilityDispatches.push(report.accessibilityDispatches[1])
  },
  'wrong localized text': report => {
    report.accessibilityDispatches[1].text = 'wrong'
  },
  'announcement before focus': report => {
    report.accessibilityDispatches.reverse()
  },
  'orphan dispatch': report => {
    report.accessibilityDispatches.push({ kind: 'focus', activationId: 3 })
  },
  'unknown dispatch': report => {
    report.accessibilityDispatches.push({
      kind: 'speech-delivered',
      activationId: 1,
      text: 'unproven',
    })
  },
  'extra dispatch field': report => {
    report.accessibilityDispatches[1].delivered = true
  },
}
for (const [name, corrupt] of Object.entries(corruptions)) {
  it(`rejects ${name}`, () => {
    let report = appReport()
    const replacement = corrupt(report)
    if (replacement === null) report = null
    assert.throws(() => validateNotificationAppEvidence(report, inputs))
  })
}

it('loads the canonical fixture and generated localization without hardcoded announcement copy', () => {
  const root = mkdtempSync(join(tmpdir(), 'notification-inputs-'))
  try {
    const resource = writeInputs(root)
    assert.deepEqual(notificationInputs(root), inputs)
    const original = readFileSync(resource, 'utf8')
    writeFileSync(resource, original.replace('&amp;', '&#38;'))
    assert.deepEqual(notificationInputs(root), inputs)
    writeFileSync(resource, original.replace('&amp;', '&#x26;'))
    assert.deepEqual(notificationInputs(root), inputs)
    for (const value of [
      original.replace('&amp;', '&unknown;'),
      original + original,
      original.replace('notificationSettingsOpened', 'missingKey'),
      original.replace('Generated &amp; localized', ''),
    ]) {
      writeFileSync(resource, value)
      assert.throws(() => notificationInputs(root))
    }
    assert.throws(() => notificationInputs('relative'))
    assert.throws(() => notificationInputs(undefined))
    writeInputs(root)
    const manifest = join(root, 'api-fixtures/v1/manifest.json')
    for (const fixture of [
      {},
      {
        id: 'native.my.email-preferences.default',
        method: 'GET',
        path: '/api/v1/my/email-preferences',
        status: 200,
        bodyFile: '../escape',
      },
    ]) {
      writeFileSync(manifest, JSON.stringify({ fixtures: [fixture] }))
      assert.throws(() => notificationInputs(root))
    }
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
