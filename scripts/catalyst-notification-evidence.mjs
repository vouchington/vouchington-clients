import { readFileSync } from 'node:fs'
import { pathToFileURL } from 'node:url'
import {
  notificationInputs,
  validateNotificationAppEvidence,
} from './catalyst-notification-app-evidence.mjs'

export const notificationChecks = [
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
]
const context = {
  schemaVersion: 2,
  platform: 'Mac Catalyst',
  runtime: 'Debug Mac2',
  scope: 'issue-60-controls-and-accessibility-dispatch',
}

export function notificationEvidence(checks, nativeRevision, producerCommit) {
  const evidence = {
    ...context,
    nativeRevision,
    producerCommit,
    checks,
    total: checks.length,
    passed: checks.filter(check => check.status === 'passed').length,
    failed: checks.filter(check => check.status === 'failed').length,
    skipped: checks.filter(check => check.status === 'skipped').length,
  }
  validateNotificationEvidence(evidence, nativeRevision, producerCommit)
  return evidence
}

export function validateNotificationEvidence(evidence, nativeRevision, producerCommit) {
  const expected = { ...context, nativeRevision, producerCommit }
  if (
    ![nativeRevision, producerCommit].every(
      value => typeof value === 'string' && /^[a-f0-9]{40}$/u.test(value),
    )
  )
    throw new Error('expected native revision and producer commit must be full SHAs')
  const keys = [...Object.keys(expected), 'checks', 'total', 'passed', 'failed', 'skipped']
  if (
    !evidence ||
    typeof evidence !== 'object' ||
    Array.isArray(evidence) ||
    Object.keys(evidence).length !== keys.length ||
    Object.keys(evidence).some(key => !keys.includes(key))
  )
    throw new Error('malformed notification evidence')
  for (const [key, value] of Object.entries(expected)) {
    if (evidence[key] !== value) throw new Error(`notification evidence ${key} mismatch`)
  }
  if (!Array.isArray(evidence.checks) || evidence.checks.length !== notificationChecks.length)
    throw new Error('notification evidence requires the current control and dispatch assertion set')
  const seen = new Set()
  for (const check of evidence.checks) {
    if (
      !check ||
      Object.keys(check).length !== 2 ||
      !Object.hasOwn(check, 'name') ||
      !Object.hasOwn(check, 'status') ||
      !notificationChecks.includes(check.name) ||
      seen.has(check.name) ||
      check.status !== 'passed'
    )
      throw new Error('invalid, duplicate, or unsuccessful notification assertion')
    seen.add(check.name)
  }
  const counts = { total: seen.size, passed: seen.size, failed: 0, skipped: 0 }
  for (const [key, value] of Object.entries(counts)) {
    if (evidence[key] !== value) throw new Error(`notification evidence ${key} mismatch`)
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const [, , result, nativeRevision] = process.argv
    const { commit } = JSON.parse(readFileSync('contracts/vouchington.json', 'utf8'))
    validateNotificationEvidence(JSON.parse(readFileSync(result, 'utf8')), nativeRevision, commit)
    validateNotificationAppEvidence(
      JSON.parse(readFileSync(`${result}.app.json`, 'utf8')),
      notificationInputs(process.env.VOUCHA_CONTRACT_ROOT),
    )
  } catch (error) {
    console.error(error instanceof Error ? error.message : error)
    process.exitCode = 1
  }
}
