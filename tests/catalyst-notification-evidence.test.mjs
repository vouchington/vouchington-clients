import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, it } from 'node:test'
import { appReport, writeInputs } from './catalyst-notification-app-fixture.mjs'
import {
  notificationChecks,
  notificationEvidence,
  validateNotificationEvidence,
} from '../scripts/catalyst-notification-evidence.mjs'

const revision = 'a'.repeat(40)
const producer = 'b'.repeat(40)
const records = () => notificationChecks.map(name => ({ name, status: 'passed' }))
const evidence = () => notificationEvidence(records(), revision, producer)

describe('Catalyst control and dispatch evidence validation', () => {
  it('derives aggregate counts from the named successful records', () => {
    assert.deepEqual(evidence(), {
      schemaVersion: 2,
      platform: 'Mac Catalyst',
      runtime: 'Debug Mac2',
      scope: 'issue-60-controls-and-accessibility-dispatch',
      nativeRevision: revision,
      producerCommit: producer,
      checks: records(),
      total: 10,
      passed: 10,
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
      value.schemaVersion = 1
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
    const root = result + '-contract'
    writeInputs(root)
    writeFileSync(`${result}.app.json`, JSON.stringify(appReport()))
    const run = () =>
      execFileSync(process.execPath, [script, result, revision], {
        stdio: 'pipe',
        env: { ...process.env, VOUCHA_CONTRACT_ROOT: root },
      })
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
    writeFileSync(`${result}.app.json`, JSON.stringify(appReport()))
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
    assert.equal(existsSync(`${result}.app.json`), false)
  })
})
