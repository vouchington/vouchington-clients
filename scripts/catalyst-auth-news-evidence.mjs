import { readFileSync } from 'node:fs'
import { pathToFileURL } from 'node:url'

export const authNewsChecks = [
  'anonymous-session-visible',
  'email-otp-request-accepted',
  'email-otp-session-authenticated',
  'news-fixture-row-visible',
  'related-article-expanded',
  'related-article-collapsed',
]

const context = {
  schemaVersion: 1,
  platform: 'Mac Catalyst',
  runtime: 'Debug Mac2',
  scope: 'issue-77-email-otp-news-ui',
  backend: 'isolated-fixture-replay',
}

export function authNewsEvidence(checks, nativeRevision, producerCommit) {
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
  validateAuthNewsEvidence(evidence, nativeRevision, producerCommit)
  return evidence
}

export function validateAuthNewsEvidence(evidence, nativeRevision, producerCommit) {
  if (
    ![nativeRevision, producerCommit].every(
      value => typeof value === 'string' && /^[a-f0-9]{40}$/u.test(value),
    )
  )
    throw new Error('expected native revision and producer commit must be full SHAs')
  const expected = { ...context, nativeRevision, producerCommit }
  const keys = [...Object.keys(expected), 'checks', 'total', 'passed', 'failed', 'skipped']
  if (
    !evidence ||
    typeof evidence !== 'object' ||
    Array.isArray(evidence) ||
    Object.keys(evidence).length !== keys.length ||
    Object.keys(evidence).some(key => !keys.includes(key))
  )
    throw new Error('malformed auth/news evidence')
  for (const [key, value] of Object.entries(expected)) {
    if (evidence[key] !== value) throw new Error(`auth/news evidence ${key} mismatch`)
  }
  if (!Array.isArray(evidence.checks) || evidence.checks.length !== authNewsChecks.length)
    throw new Error('auth/news evidence requires six named runtime assertions')
  const seen = new Set()
  for (const check of evidence.checks) {
    if (
      !check ||
      Object.keys(check).length !== 2 ||
      !Object.hasOwn(check, 'name') ||
      !Object.hasOwn(check, 'status') ||
      !authNewsChecks.includes(check.name) ||
      seen.has(check.name) ||
      check.status !== 'passed'
    )
      throw new Error('invalid, duplicate, or unsuccessful auth/news assertion')
    seen.add(check.name)
  }
  const counts = { total: seen.size, passed: seen.size, failed: 0, skipped: 0 }
  for (const [key, value] of Object.entries(counts)) {
    if (evidence[key] !== value) throw new Error(`auth/news evidence ${key} mismatch`)
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const [, , result, nativeRevision] = process.argv
    const { commit } = JSON.parse(readFileSync('contracts/vouchington.json', 'utf8'))
    validateAuthNewsEvidence(JSON.parse(readFileSync(result, 'utf8')), nativeRevision, commit)
  } catch (error) {
    console.error(error instanceof Error ? error.message : error)
    process.exitCode = 1
  }
}
