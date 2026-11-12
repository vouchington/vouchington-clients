import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  authNewsChecks,
  authNewsEvidence,
  validateAuthNewsEvidence,
} from '../scripts/catalyst-auth-news-evidence.mjs'

const revision = 'a'.repeat(40)
const { commit } = JSON.parse(
  readFileSync(new URL('../contracts/vouchington.json', import.meta.url), 'utf8'),
)
const checks = () => authNewsChecks.map(name => ({ name, status: 'passed' }))
const evidence = () => authNewsEvidence(checks(), revision, commit)

describe('Catalyst auth/news runtime evidence contract (not native execution)', () => {
  it('requires the six exact successful assertions and immutable revisions', () => {
    const result = evidence()
    assert.equal(result.scope, 'issue-77-email-otp-news-ui')
    assert.equal(result.backend, 'isolated-fixture-replay')
    assert.equal(result.total, 6)
    assert.equal(result.passed, 6)
    validateAuthNewsEvidence(result, revision, commit)
  })

  for (const invalid of [undefined, null, '', 'short', 'A'.repeat(40), 123]) {
    it(`rejects invalid expected revisions: ${String(invalid)}`, () => {
      assert.throws(() => validateAuthNewsEvidence(evidence(), invalid, commit), /full SHAs/u)
      assert.throws(() => validateAuthNewsEvidence(evidence(), revision, invalid), /full SHAs/u)
    })
  }

  for (const value of [null, [], 'result', 1, {}, { ...evidence(), extra: true }]) {
    it(`rejects malformed evidence ${JSON.stringify(value)}`, () => {
      assert.throws(() => validateAuthNewsEvidence(value, revision, commit), /malformed/u)
    })
  }

  for (const key of [
    'schemaVersion',
    'platform',
    'runtime',
    'scope',
    'backend',
    'nativeRevision',
    'producerCommit',
  ]) {
    it(`rejects a stale or different ${key}`, () => {
      assert.throws(
        () => validateAuthNewsEvidence({ ...evidence(), [key]: 'different' }, revision, commit),
        /mismatch/u,
      )
    })
  }

  for (const invalid of [null, [], checks().slice(1)]) {
    it(`rejects an incomplete check set ${JSON.stringify(invalid)}`, () => {
      assert.throws(
        () => validateAuthNewsEvidence({ ...evidence(), checks: invalid }, revision, commit),
        /six named/u,
      )
    })
  }

  for (const invalid of [
    null,
    {},
    { name: authNewsChecks[0] },
    { name: 'unknown', status: 'passed' },
    { name: authNewsChecks[0], status: 'failed' },
    { name: authNewsChecks[0], status: 'skipped' },
    { name: authNewsChecks[0], status: 'passed', extra: true },
  ]) {
    it(`rejects an invalid or unsuccessful assertion ${JSON.stringify(invalid)}`, () => {
      const result = evidence()
      result.checks[0] = invalid
      assert.throws(() => validateAuthNewsEvidence(result, revision, commit), /assertion/u)
    })
  }

  it('rejects duplicate names instead of accepting aggregate counts', () => {
    const result = evidence()
    result.checks[1] = result.checks[0]
    assert.throws(() => validateAuthNewsEvidence(result, revision, commit), /duplicate/u)
    assert.throws(
      () =>
        authNewsEvidence(
          checks().map(check => ({ ...check, status: 'failed' })),
          revision,
          commit,
        ),
      /assertion/u,
    )
    assert.throws(
      () =>
        authNewsEvidence(
          checks().map(check => ({ ...check, status: 'skipped' })),
          revision,
          commit,
        ),
      /assertion/u,
    )
  })

  for (const key of ['total', 'passed', 'failed', 'skipped']) {
    it(`rejects forged ${key} counts`, () => {
      assert.throws(
        () => validateAuthNewsEvidence({ ...evidence(), [key]: -1 }, revision, commit),
        /mismatch/u,
      )
    })
  }

  it('runs the CLI against the checkout pin and rejects stale, missing, or malformed results', () => {
    const directory = mkdtempSync(join(tmpdir(), 'auth-news-evidence-'))
    try {
      const result = join(directory, 'result.json')
      const script = fileURLToPath(
        new URL('../scripts/catalyst-auth-news-evidence.mjs', import.meta.url),
      )
      const run = (...args) => spawnSync(process.execPath, [script, ...args], { encoding: 'utf8' })
      writeFileSync(result, JSON.stringify(evidence()))
      assert.equal(run(result, revision).status, 0)
      assert.notEqual(run(result, 'b'.repeat(40)).status, 0)
      writeFileSync(result, '{')
      assert.notEqual(run(result, revision).status, 0)
      assert.notEqual(run(join(directory, 'missing.json'), revision).status, 0)
      assert.notEqual(run().status, 0)
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  })
})
