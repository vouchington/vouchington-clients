import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { notificationLocalizationFile } from '../scripts/catalyst-notification-app-evidence.mjs'

export const inputs = {
  preferences: {
    is_engagement_emails_enabled: true,
    news_digest_frequency: 'weekly',
    is_moderation_emails_enabled: true,
    community_digest_frequency: 'weekly',
    moderation_email_cadence: 'daily',
    moderation_email_days_of_week: [1, 2, 3],
    moderation_email_time_of_day: '09:00',
    moderation_email_timezone: 'America/Los_Angeles',
  },
  announcement: 'Generated & localized',
}

export function appReport() {
  const report = {
    schemaVersion: 1,
    initialPreferences: structuredClone(inputs.preferences),
    patches: [],
    activations: [],
    accessibilityDispatches: [],
  }
  const values = [false, 'daily', false, 'daily', 'selected_days', [1, 3], '09:30', 'UTC']
  let state = structuredClone(inputs.preferences)
  for (const [index, field] of Object.keys(state).entries()) {
    const after = { ...state, [field]: values[index] }
    report.patches.push({ fields: [field], before: state, after })
    state = after
  }
  for (const activationId of [1, 2]) {
    report.activations.push({ activationId, canonicalPath: '/my/notification-settings' })
    report.accessibilityDispatches.push(
      {
        kind: 'focus',
        activationId,
        automationId: 'notification-preferences-heading',
        succeeded: true,
      },
      { kind: 'announcement', activationId, text: inputs.announcement },
    )
  }
  return report
}

export function writeInputs(root) {
  const base = join(root, 'api-fixtures/v1')
  mkdirSync(join(base, 'responses'), { recursive: true })
  writeFileSync(
    join(base, 'manifest.json'),
    JSON.stringify({
      fixtures: [
        {
          id: 'native.my.email-preferences.default',
          method: 'GET',
          path: '/api/v1/my/email-preferences',
          status: 200,
          bodyFile: 'responses/preferences.json',
        },
      ],
    }),
  )
  writeFileSync(
    join(base, 'responses/preferences.json'),
    JSON.stringify({ email_preferences: inputs.preferences }),
  )
  const resource = join(root, notificationLocalizationFile)
  mkdirSync(dirname(resource), { recursive: true })
  writeFileSync(
    resource,
    '<root><data name="native.dotnet.settings.notificationSettingsOpened" xml:space="preserve"><value>Generated &amp; localized</value></data></root>',
  )
  return resource
}
