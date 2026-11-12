import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { assertNotificationControls } from '../dotnet-clients/tooling/catalyst-gui-notification-session.mjs'
import {
  notificationChecks,
  notificationEvidence,
  validateNotificationEvidence,
} from '../scripts/catalyst-notification-evidence.mjs'

const revision = 'a'.repeat(40)
const producer = 'b'.repeat(40)
const records = () => notificationChecks.map(name => ({ name, status: 'passed' }))
const evidence = () => notificationEvidence(records(), revision, producer)

function mockRequest({ moderation = true, engagement = false, failNews = false } = {}) {
  let clicked = false
  const operations = []
  const request = async (method, path, body) => {
    operations.push({ method, path, body })
    if (body?.using === 'accessibility id') return { value: { ELEMENT: body.value } }
    if (path.endsWith('/click')) {
      clicked = true
      return {}
    }
    if (failNews && path.includes('notification-news-digest'))
      throw new Error('news request failed')
    if (path.endsWith('/attribute/enabled')) return { value: true }
    if (path.includes('notification-moderation')) return { value: moderation }
    if (path.includes('notification-engagement'))
      return { value: clicked ? !engagement : engagement }
    throw new Error(`unexpected request ${method} ${path}`)
  }
  return { request, operations }
}

describe('current partial Catalyst notification assertions', () => {
  for (const engagement of [true, false]) {
    it(`records successful assertions after toggling engagement from ${engagement}`, async () => {
      const { request, operations } = mockRequest({ engagement })
      const checks = await assertNotificationControls(request, 'session')
      assert.deepEqual(checks, records())
      assert.equal(operations.filter(item => item.path.endsWith('/click')).length, 1)
      assert.equal(
        operations.filter(
          item =>
            item.path.endsWith('/attribute/value') && item.path.includes('notification-engagement'),
        ).length,
        3,
      )
      validateNotificationEvidence(
        notificationEvidence(checks, revision, producer),
        revision,
        producer,
      )
    })
  }

  it('does not return records or continue after failed moderation', async () => {
    const { request, operations } = mockRequest({ moderation: false })
    let checks
    await assert.rejects(async () => {
      checks = await assertNotificationControls(request, 'session')
    }, /expected on/u)
    assert.equal(checks, undefined)
    assert.equal(
      operations.some(item => item.body?.value === 'notification-engagement'),
      false,
    )
  })

  it('does not return completion records when the final request fails', async () => {
    let checks
    await assert.rejects(async () => {
      checks = await assertNotificationControls(mockRequest({ failNews: true }).request, 'session')
    }, /news request failed/u)
    assert.equal(checks, undefined)
  })
})

describe('Catalyst partial evidence validation', () => {
  it('derives aggregate counts from the named successful records', () => {
    assert.deepEqual(evidence(), {
      schemaVersion: 1,
      platform: 'Mac Catalyst',
      runtime: 'Debug Mac2',
      scope: 'issue-91-partial-notification-controls',
      nativeRevision: revision,
      producerCommit: producer,
      checks: records(),
      total: 4,
      passed: 4,
      failed: 0,
      skipped: 0,
    })
  })

  const cases = {
    missing: value => value.checks.pop(),
    duplicate: value => {
      value.checks[3] = value.checks[0]
    },
    unknown: value => {
      value.checks[0].name = 'all-eight-scenarios'
    },
    failed: value => {
      value.checks[0].status = 'failed'
    },
    skipped: value => {
      value.checks[0].status = 'skipped'
    },
    malformed: value => {
      value.checks[0] = null
    },
    'extra claim': value => {
      value.complete91 = true
    },
    'extra assertion claim': value => {
      value.checks[0].physicalDevice = true
    },
    'wrong platform': value => {
      value.platform = 'physical iPhone'
    },
    'wrong runtime': value => {
      value.runtime = 'signed Release'
    },
    'wrong scope': value => {
      value.scope = 'issue-91-complete'
    },
    'stale revision': value => {
      value.nativeRevision = 'c'.repeat(40)
    },
    'cross pin': value => {
      value.producerCommit = 'd'.repeat(40)
    },
    'wrong total': value => {
      value.total = 3
    },
    'wrong passed': value => {
      value.passed = 3
    },
    'wrong failed': value => {
      value.failed = 1
    },
    'wrong skipped': value => {
      value.skipped = 1
    },
    'wrong schema': value => {
      value.schemaVersion = 2
    },
  }
  for (const [name, mutate] of Object.entries(cases)) {
    it(`rejects ${name}`, () => {
      const value = evidence()
      mutate(value)
      assert.throws(() => validateNotificationEvidence(value, revision, producer))
    })
  }
  it('rejects missing and malformed expected revisions', () => {
    for (const sha of [undefined, '', 'main', 'a'.repeat(39), ['a'.repeat(40)]]) {
      assert.throws(() => validateNotificationEvidence(evidence(), sha, producer))
      assert.throws(() => validateNotificationEvidence(evidence(), revision, sha))
    }
  })
})

describe('Catalyst evidence command line', () => {
  it('accepts current evidence and rejects stale, wrong-pin and malformed files', () => {
    const result = join(mkdtempSync(join(tmpdir(), 'catalyst-evidence-')), 'result.json')
    const script = fileURLToPath(
      new URL('../scripts/catalyst-notification-evidence.mjs', import.meta.url),
    )
    const { commit } = JSON.parse(
      readFileSync(new URL('../contracts/vouchington.json', import.meta.url), 'utf8'),
    )
    const run = () => execFileSync(process.execPath, [script, result, revision], { stdio: 'pipe' })
    writeFileSync(result, JSON.stringify(notificationEvidence(records(), revision, commit)))
    run()
    writeFileSync(result, JSON.stringify(notificationEvidence(records(), 'c'.repeat(40), commit)))
    assert.throws(run, /nativeRevision mismatch/u)
    writeFileSync(result, JSON.stringify(evidence()))
    assert.throws(run, /producerCommit mismatch/u)
    writeFileSync(result, '{')
    assert.throws(run)
  })

  it('removes a previous success result before a failed session run', () => {
    const result = join(mkdtempSync(join(tmpdir(), 'catalyst-failed-')), 'result.json')
    writeFileSync(result, JSON.stringify(evidence()))
    const script = fileURLToPath(
      new URL('../dotnet-clients/tooling/catalyst-gui-preflight.mjs', import.meta.url),
    )
    assert.throws(() =>
      execFileSync(process.execPath, [script, '--session'], {
        env: {
          ...process.env,
          CATALYST_GUI_RESULT: result,
          CATALYST_APP_PATH: '/missing/app',
          CATALYST_NATIVE_REVISION: revision,
        },
        stdio: 'pipe',
      }),
    )
    assert.equal(existsSync(result), false)
  })
})
