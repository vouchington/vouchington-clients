import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it } from 'node:test'
import { readResults, shortClassName } from '../scripts/test-plan-shadow-results.mjs'
import { jobBlock } from './workflow-job-block.mjs'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const action = await readFile(
  new URL('../.github/actions/test-plan-shadow/action.yml', import.meta.url),
  'utf8',
)

describe('shadow test planning', () => {
  it('runs only on pull requests, never blocks, and covers the unit suites', () => {
    for (const job of [
      'test-swift-core',
      'test-swift-ui',
      'dotnet-portable',
      'dotnet-maui-tests',
    ]) {
      const block = jobBlock(workflow, job, `missing job ${job}`)
      const step = block.slice(block.indexOf('- name: Shadow test plan (advisory)'))
      assert.match(
        step,
        /if: \$\{\{ github\.event_name == 'pull_request' && !cancelled\(\) \}\}/u,
        job,
      )
      assert.match(step, /continue-on-error: true/u, job)
      assert.match(step, /timeout-minutes: 2/u, job)
      assert.match(step, /uses: \.\/\.github\/actions\/test-plan-shadow/u, job)
    }
    assert.equal(workflow.match(/Shadow test plan \(advisory\)/gu).length, 4)
  })

  it('keeps every action step non-blocking', () => {
    assert.equal(action.match(/continue-on-error: true/gu).length, 2)
  })

  it('reads JUnit and TRX results into per-class failures', () => {
    const dir = mkdtempSync(join(tmpdir(), 'shadow-'))
    writeFileSync(
      join(dir, 'a.xml'),
      '<testcase classname="Mod.OkTests" name="a"/><testcase classname="Mod.BadTests" name="b"><failure/></testcase>',
    )
    writeFileSync(
      join(dir, 'b.trx'),
      '<UnitTestResult testId="1" outcome="Failed"/><UnitTest id="1"><TestMethod className="Ns.TrxTests, Asm" name="x"/></UnitTest>',
    )
    const results = readResults(dir)
    assert.deepEqual(results.get('OkTests'), { failed: false })
    assert.deepEqual(results.get('BadTests'), { failed: true })
    assert.deepEqual(results.get('TrxTests'), { failed: true })
    assert.equal(shortClassName('Ns.Class, Asm'), 'Class')
  })
})
