import assert from 'node:assert/strict'
import childProcess from 'node:child_process'
import { syncBuiltinESMExports } from 'node:module'
import test from 'node:test'
import {
  inventoryTrackedSourceMetadata,
  restoreTrackedSourceTimes,
  sourceMetadataLimits,
} from '../swift-clients/apps/android/tooling/android-state-source-metadata.mjs'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'

for (const [operation, invoke, message] of [
  [
    'inventory',
    inventoryTrackedSourceMetadata,
    /tracked inventory exceeded bounds before helper launch/,
  ],
  ['replay', restoreTrackedSourceTimes, /timestamp replay exceeded bounds before helper launch/],
]) {
  test(`${operation} rejects an exhausted launch budget before timeout zero can disable the cap`, async t => {
    const state = await androidStateFixture(t)
    const metadata = await inventoryTrackedSourceMetadata(state)
    const originalSpawn = childProcess.spawnSync
    const times = [0, 0, sourceMetadataLimits.scanMs]
    let nowCalls = 0
    let pythonCalls = 0
    t.mock.method(Date, 'now', () => times[nowCalls++] ?? sourceMetadataLimits.scanMs)
    t.mock.method(childProcess, 'spawnSync', (command, args, options) => {
      if (command !== 'python3') return originalSpawn(command, args, options)
      pythonCalls++
      throw new Error(`Python must not launch with timeout ${options.timeout}`)
    })
    syncBuiltinESMExports()
    try {
      await assert.rejects(invoke({ ...state, metadata }), message)
    } finally {
      t.mock.restoreAll()
      syncBuiltinESMExports()
    }
    assert.equal(nowCalls, 3)
    assert.equal(pythonCalls, 0)
  })
}
