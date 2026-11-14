import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { runAndroidSession } from '../scripts/paired-native-android-session.mjs'
import { validateCleanupEvidence } from '../scripts/paired-native-cleanup-evidence.mjs'
import { lineageLimits } from '../scripts/paired-native-emulator-lineage.mjs'
import { observeOwnedEmulatorLineage } from '../scripts/paired-native-proc-snapshot.mjs'
import { readBoundedFile } from '../scripts/paired-native-proc-read.mjs'
import { passingAdbCommand } from './paired-native-adb-command.mjs'
import { cleanupBinding, cleanupFixture } from './paired-native-cleanup-fixture.mjs'
import {
  lineageFixtureAvd as avd,
  procStatText as statText,
  selectLineageFixture as select,
} from './paired-native-lineage-fixture.mjs'

function record(pid, ppid, pgid, command, startTime) {
  return { pid, ppid, pgid, startTime, identity: { command } }
}

it('rejects an observed launch without the captured launcher or a connected ancestry', () => {
  const childrenOnly = select([record(201, 200, 200, 'qemu-system-x86_64', 201)])
  assert.deepEqual(childrenOnly, { state: 'unknown', launcherPid: 200, reason: 'missing' })
  assert.equal(Object.hasOwn(childrenOnly, 'processes'), false)
  assert.throws(
    () =>
      validateCleanupEvidence(
        cleanupFixture({
          launch: { state: 'observed', launcherPid: 100, processes: [] },
        }),
        cleanupBinding(),
      ),
    /captured launcher/u,
  )
  assert.throws(
    () =>
      validateCleanupEvidence(
        cleanupFixture({
          launch: {
            state: 'observed',
            launcherPid: 100,
            processes: [
              {
                pid: 100,
                ppid: 10,
                pgid: 100,
                command: 'emulator -avd paired-native-42',
              },
              { pid: 300, ppid: 1, pgid: 300, command: 'qemu-system-x86_64' },
            ],
          },
        }),
        cleanupBinding(),
      ),
    /ancestry is disconnected/u,
  )
})

it('does not treat a reused pid with a valid command as the owned process', () => {
  const reused = select([record(201, 200, 200, 'qemu-system-x86_64', 900)], {
    priorIdentities: [{ pid: 201, startTime: 201 }],
    claimAbsent: true,
  })
  assert.deepEqual(reused, { state: 'unknown', launcherPid: 200, reason: 'ambiguous' })
  assert.equal(Object.hasOwn(reused, 'processes'), false)
  const differentParent = select([record(201, 1, 201, 'qemu-system-x86_64', 900)], {
    priorIdentities: [{ pid: 201, startTime: 100 }],
    claimAbsent: true,
  })
  assert.equal(differentParent.reason, 'ambiguous')
  const same = select([record(201, 200, 200, 'qemu-system-x86_64', 201)], {
    priorIdentities: [{ pid: 201, startTime: 201 }],
    claimAbsent: true,
  })
  assert.equal(same.state, 'observed')
  assert.deepEqual(
    same.processes.map(entry => entry.pid),
    [201],
  )
})

it('bounds proc bytes and turns a stalled scan into unknown', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-bytes-'))
  const path = join(root, 'stat')
  await writeFile(path, Buffer.alloc(4000, 0x61))
  try {
    const bytes = await readBoundedFile(path, 32)
    assert.equal(bytes.length, 32)
    const lengths = []
    const started = Date.now()
    const stalledDirectory = await observeOwnedEmulatorLineage({
      launcherPid: 200,
      avdName: avd,
      limits: { ...lineageLimits, maxDurationMs: 40 },
      opendir: () => new Promise(() => {}),
    })
    assert.deepEqual(stalledDirectory, { state: 'unknown', launcherPid: 200, reason: 'bounded' })
    const stalledRead = await observeOwnedEmulatorLineage({
      launcherPid: 200,
      avdName: avd,
      limits: { ...lineageLimits, maxDurationMs: 40 },
      now: () => 0,
      opendir: async () => ({
        async *[Symbol.asyncIterator]() {
          yield { name: '200' }
        },
        async close() {},
      }),
      readFile: (_path, options) => {
        lengths.push(options.length)
        return new Promise(() => {})
      },
    })
    assert.equal(stalledRead.reason, 'bounded')
    assert.equal(lengths[0], 1024)
    assert.ok(Date.now() - started < 1000)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('does not let a stalled lineage scan block emulator cleanup', async () => {
  const signals = []
  const runSyncCalls = []
  const child = new EventEmitter()
  child.pid = 200
  child.exitCode = null
  child.signalCode = null
  child.kill = signal => {
    signals.push(signal)
    child.exitCode = 0
    child.emit('exit', 0, signal)
  }
  let saved
  const started = Date.now()
  await runAndroidSession({
    emulatorPath: '/emulator',
    avdName: avd,
    runId: '55',
    nativeTestProfile: 'cookie-snapshot',
    clientsMergeSha: 'd'.repeat(40),
    apkSha256: 'e'.repeat(64),
    appApk: '/tmp/app-debug.apk',
    appId: 'ai.voucha.android',
    testId: 'ai.voucha.android.test',
    emulatorVersion: 'Android emulator version 37.2.12.0 (build_id 16428233)',
    logPath: '/tmp/emulator.log',
    resultPath: '/tmp/runtime-result.json',
    runCommand: passingAdbCommand,
    runSync: (program, args) => {
      runSyncCalls.push({ program, args })
      return { status: 0 }
    },
    launch: () => child,
    openLog: () => ({ fd: 1, end() {} }),
    saveFile: async (_path, contents) => {
      saved = contents
    },
    makeDirectory: async () => {},
    sleep: async () => {},
    observeLineage: () => new Promise(() => {}),
  })
  assert.ok(Date.now() - started < 8000)
  assert.deepEqual(signals, ['SIGTERM'])
  assert.deepEqual(runSyncCalls, [{ program: 'adb', args: ['emu', 'kill'] }])
  const result = JSON.parse(saved)
  assert.equal(result.cleanup.launch.state, 'unknown')
  assert.equal(result.cleanup.launch.reason, 'bounded')
  assert.equal(result.cleanup.shutdown.reason, 'bounded')
  assert.equal(Object.hasOwn(result.cleanup.launch, 'processes'), false)
})

it('accepts the execed qemu backend only at the captured launcher pid', () => {
  const qemu = select([record(200, 10, 200, `qemu-system-x86_64 -avd ${avd}`, 200)])
  assert.equal(qemu.state, 'observed')
  assert.equal(qemu.processes[0].command, `qemu-system-x86_64 -avd ${avd}`)
  const headless = select([record(200, 10, 200, `qemu-system-x86_64-headless -avd ${avd}`, 200)])
  assert.equal(headless.state, 'observed')
  assert.equal(select([record(200, 10, 200, 'qemu-system-x86_64', 200)]).reason, 'ambiguous')
  assert.equal(select([record(200, 10, 200, 'crashpad_handler', 200)]).reason, 'ambiguous')
  assert.equal(
    validateCleanupEvidence(
      cleanupFixture({
        launch: {
          state: 'observed',
          launcherPid: 100,
          processes: [
            {
              pid: 100,
              ppid: 10,
              pgid: 100,
              command: 'qemu-system-x86_64 -avd paired-native-42',
            },
          ],
        },
      }),
      cleanupBinding(),
    ),
    true,
  )
  assert.throws(
    () =>
      validateCleanupEvidence(
        cleanupFixture({
          shutdown: { state: 'observed', launcherPid: 101, processes: [] },
        }),
        cleanupBinding(),
      ),
    /launcher pid differs/u,
  )
})

it('rejects a command read after the pid start time changes', async () => {
  const stats = [statText(200, 10, 200, 200), statText(200, 1, 200, 900)]
  let statReads = 0
  const directory = async () => ({
    async *[Symbol.asyncIterator]() {
      yield { name: '200' }
    },
    async close() {},
  })
  const reused = await observeOwnedEmulatorLineage({
    launcherPid: 200,
    avdName: avd,
    limits: lineageLimits,
    now: () => 0,
    opendir: directory,
    readFile: async path => {
      if (path.endsWith('/stat')) return Buffer.from(stats[statReads++] ?? stats.at(-1))
      return Buffer.from(`qemu-system-x86_64\0-avd\0${avd}\0`)
    },
  })
  assert.deepEqual(reused, { state: 'unknown', launcherPid: 200, reason: 'ambiguous' })
  assert.equal(Object.hasOwn(reused, 'processes'), false)
  assert.equal(statReads, 2)
  const stableStats = [statText(200, 10, 200, 200), statText(200, 10, 200, 200)]
  let stableReads = 0
  const stable = await observeOwnedEmulatorLineage({
    launcherPid: 200,
    avdName: avd,
    limits: lineageLimits,
    now: () => 0,
    opendir: directory,
    readFile: async path => {
      if (path.endsWith('/stat')) return Buffer.from(stableStats[stableReads++])
      return Buffer.from(`qemu-system-x86_64\0-avd\0${avd}\0`)
    },
  })
  assert.equal(stable.state, 'observed')
  assert.equal(stable.processes[0].command, `qemu-system-x86_64 -avd ${avd}`)
  assert.equal(stableReads, 2)
})
