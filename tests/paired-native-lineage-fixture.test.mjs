import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import { parseProcStat } from '../scripts/paired-native-emulator-lineage.mjs'
import {
  lineageFixtureAvd,
  procStatText,
  selectLineageFixture,
} from './paired-native-lineage-fixture.mjs'

const callers = ['paired-native-emulator-lineage.test.mjs', 'paired-native-lineage-bounds.test.mjs']

test('places ppid, pgid, and starttime where parseProcStat reads them', () => {
  const parsed = parseProcStat(procStatText(200, 10, 200, 77, 'qemu-system-x86_64'))
  assert.deepEqual(parsed, { pid: 200, ppid: 10, pgid: 200, startTime: 77 })
  assert.equal(procStatText(200, 10, 200, 200).includes('(emulator)'), true)
})

test('selects the shared launcher and lets caller options override defaults', () => {
  const command = `emulator -avd ${lineageFixtureAvd}`
  const record = {
    pid: 200,
    ppid: 10,
    pgid: 200,
    startTime: 200,
    identity: { command },
  }
  const observed = selectLineageFixture([record])
  assert.equal(observed.state, 'observed')
  assert.equal(observed.launcherPid, 200)
  assert.equal(observed.processes[0].command, command)
  const bounded = selectLineageFixture([record], { scanComplete: false, scanReason: 'bounded' })
  assert.deepEqual(bounded, { state: 'unknown', launcherPid: 200, reason: 'bounded' })
})

test('lineage tests share the proc stat text and launcher selection', async () => {
  for (const caller of callers) {
    const source = await readFile(new URL(`./${caller}`, import.meta.url), 'utf8')
    assert.match(source, /procStatText/u)
    assert.match(source, /selectLineageFixture/u)
    assert.doesNotMatch(source, /function statText/u)
    assert.doesNotMatch(source, /Array\.from\(\{ length: 20 \}/u)
    assert.doesNotMatch(source, /function select\(/u)
  }
})
