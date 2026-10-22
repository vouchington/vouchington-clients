import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { mkdtemp, opendir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { runAndroidSession } from '../scripts/paired-native-android-session.mjs'
import { validateCleanupEvidence } from '../scripts/paired-native-cleanup-evidence.mjs'
import {
  commandIdentity,
  lineageLimits,
  ownedAvdName,
  parseProcStat,
  stopOwnedLauncher,
} from '../scripts/paired-native-emulator-lineage.mjs'
import { selectOwnedLineage } from '../scripts/paired-native-ownership.mjs'
import { observeOwnedEmulatorLineage } from '../scripts/paired-native-proc-snapshot.mjs'
import { cleanupBinding, cleanupFixture } from './paired-native-cleanup-fixture.mjs'

const avd = ownedAvdName('55')
const launcherCommand = `emulator -avd ${avd}`

function record(pid, ppid, pgid, identity, startTime = pid) {
  return { pid, ppid, pgid, startTime, identity }
}

function statText(pid, ppid, pgid, startTime, comm = 'emulator') {
  const fields = Array.from({ length: 20 }, () => '0')
  fields[0] = 'S'
  fields[1] = String(ppid)
  fields[2] = String(pgid)
  fields[19] = String(startTime)
  return `${pid} (${comm}) ${fields.join(' ')}\n`
}

function select(records, options = {}) {
  return selectOwnedLineage({
    launcherPid: 200,
    avdName: avd,
    records,
    scanComplete: true,
    limits: lineageLimits,
    ...options,
  })
}

const ownedTree = [
  record(50, 1, 50, { reason: 'ambiguous' }),
  record(77, 1, 77, { command: 'emulator -avd paired-native-999' }),
  record(200, 10, 200, { command: launcherCommand }),
  record(201, 200, 200, { command: 'qemu-system-x86_64' }),
  record(202, 201, 200, { command: 'crashpad_handler' }),
]

it('selects only the captured launcher and its descendants', () => {
  const snapshot = select(ownedTree)
  assert.equal(snapshot.state, 'observed')
  assert.deepEqual(
    snapshot.processes.map(entry => entry.pid),
    [200, 201, 202],
  )
  assert.deepEqual(snapshot.processes[0], {
    pid: 200,
    ppid: 10,
    pgid: 200,
    command: launcherCommand,
  })
  const encoded = JSON.stringify(snapshot)
  assert.equal(encoded.includes('paired-native-999'), false)
  assert.equal(encoded.includes('ambiguous'), false)
})

it('does not treat a bounded or incomplete scan as an empty owned tree', () => {
  const bounded = select(ownedTree, { limits: { ...lineageLimits, maxProcesses: 2 } })
  assert.deepEqual(bounded, { state: 'unknown', launcherPid: 200, reason: 'bounded' })
  assert.equal(Object.hasOwn(bounded, 'processes'), false)
  const truncated = select(ownedTree, { scanComplete: false, scanReason: 'bounded' })
  assert.equal(truncated.reason, 'bounded')
  assert.equal(Object.hasOwn(truncated, 'processes'), false)
  const oversized = select(ownedTree, { limits: { ...lineageLimits, maxSerializedBytes: 8 } })
  assert.equal(oversized.reason, 'bounded')
})

it('reports a missing launcher as unknown instead of an empty observation', () => {
  const unrelated = ownedTree.filter(
    entry => entry.pid !== 200 && entry.pid !== 201 && entry.pid !== 202,
  )
  const missing = select(unrelated)
  assert.deepEqual(missing, { state: 'unknown', launcherPid: 200, reason: 'missing' })
  const absent = select(unrelated, {
    claimAbsent: true,
    priorIdentities: [
      { pid: 200, startTime: 200 },
      { pid: 201, startTime: 201 },
    ],
  })
  assert.deepEqual(absent, { state: 'observed', launcherPid: 200, processes: [] })
})

it('reports ambiguous, truncated, malformed, and reused-pid ancestry as unknown', () => {
  assert.equal(
    select([...ownedTree, record(200, 11, 200, { command: launcherCommand })]).reason,
    'ambiguous',
  )
  assert.equal(
    select([
      record(200, 201, 200, { command: launcherCommand }),
      record(201, 200, 200, { command: 'qemu-system-x86_64' }),
    ]).reason,
    'ambiguous',
  )
  assert.equal(select([record(200, 10, 200, { reason: 'truncated' })]).reason, 'truncated')
  assert.equal(select([record(200, -1, 200, { command: launcherCommand })]).reason, 'malformed')
  const reused = select([record(50, 1, 50, { reason: 'ambiguous' })], {
    priorIdentities: [{ pid: 50, startTime: 50 }],
    claimAbsent: true,
  })
  assert.equal(reused.reason, 'ambiguous')
  assert.equal(JSON.stringify(reused).includes('adb'), false)
})

it('keeps a completed allowlisted command and rejects a cut AVD token', () => {
  const complete = Buffer.from(`emulator\0-avd\0${avd}\0-no-window\0-gpu\0swiftshader_indirect`)
  const identity = commandIdentity(complete, avd, Buffer.from(`emulator\0-avd\0${avd}\0`).length)
  assert.deepEqual(identity, { command: launcherCommand })
  const cut = Buffer.from(`emulator\0-avd\0paired-native-5`)
  assert.equal(commandIdentity(cut, avd, cut.length - 1).reason, 'truncated')
  assert.equal(
    commandIdentity(Buffer.from('/usr/bin/adb\0start-server\0'), avd, 64).reason,
    'ambiguous',
  )
  const parsed = parseProcStat(statText(200, 10, 200, 77, 'qemu-system-x86_64'))
  assert.equal(parsed.ppid, 10)
  assert.equal(parsed.pgid, 200)
  assert.equal(parsed.startTime, 77)
  assert.equal(parseProcStat('200 (qemu-system-x86_64) S 10 200 200 0 0\n'), null)
  assert.equal(parseProcStat('not a stat'), null)
})

it('reads command lines only for the owned pid set and bounds the scan', async () => {
  const stats = {
    50: statText(50, 1, 50, 50, 'adb'),
    200: statText(200, 10, 200, 200),
    201: statText(201, 200, 200, 201, 'qemu-system-x86_64'),
  }
  const commands = {
    200: Buffer.from(`emulator\0-avd\0${avd}\0`),
    201: Buffer.from('qemu-system-x86_64\0-accel\0on\0'),
  }
  const snapshot = await observeOwnedEmulatorLineage({
    launcherPid: 200,
    avdName: avd,
    limits: lineageLimits,
    now: () => 0,
    opendir: async () => ({
      async *[Symbol.asyncIterator]() {
        yield { name: 'cpuinfo' }
        yield { name: '50' }
        yield { name: '200' }
        yield { name: '201' }
      },
      async close() {},
    }),
    readFile: async path => {
      const pid = path.split('/')[2]
      if (path.endsWith('/cmdline') && pid === '50') throw new Error('unrelated cmdline was read')
      if (path.endsWith('/stat')) return Buffer.from(stats[pid])
      return commands[pid]
    },
  })
  assert.deepEqual(
    snapshot.processes.map(entry => entry.pid),
    [200, 201],
  )
  let clock = 0
  const timed = await observeOwnedEmulatorLineage({
    launcherPid: 200,
    avdName: avd,
    limits: { ...lineageLimits, maxDurationMs: 500 },
    now: () => clock,
    opendir: async () => ({
      async *[Symbol.asyncIterator]() {
        yield { name: '200' }
        yield { name: '201' }
      },
      async close() {},
    }),
    readFile: async path => {
      clock += 600
      const pid = path.split('/')[2]
      return Buffer.from(statText(pid, 10, pid, 10))
    },
  })
  assert.deepEqual(timed, { state: 'unknown', launcherPid: 200, reason: 'bounded' })
  const unavailable = await observeOwnedEmulatorLineage({
    launcherPid: 200,
    avdName: avd,
    opendir: async () => {
      throw new Error('no proc')
    },
  })
  assert.equal(unavailable.reason, 'missing')
  const malformed = await observeOwnedEmulatorLineage({
    launcherPid: 200,
    avdName: avd,
    now: () => 0,
    opendir: async () => ({
      async *[Symbol.asyncIterator]() {
        yield { name: '200' }
      },
      async close() {},
    }),
    readFile: async () => Buffer.from('not-a-stat'),
  })
  assert.equal(malformed.reason, 'malformed')
})

it('keeps an observed scan when the process directory is already closed', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-proc-'))
  await writeFile(join(root, '200'), '')
  try {
    const snapshot = await observeOwnedEmulatorLineage({
      launcherPid: 200,
      avdName: avd,
      limits: lineageLimits,
      now: () => 0,
      opendir: () => opendir(root),
      readFile: async path => {
        if (path.endsWith('/cmdline') && path.split('/')[2] !== '200')
          throw new Error('unrelated cmdline was read')
        return path.endsWith('/stat')
          ? Buffer.from(statText(200, 10, 200, 200))
          : Buffer.from(`emulator\0-avd\0${avd}\0`)
      },
    })
    assert.equal(snapshot.state, 'observed')
    assert.deepEqual(
      snapshot.processes.map(entry => entry.pid),
      [200],
    )
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('keeps the owned stop budgets and signals only that child', async () => {
  const signals = []
  const sleeps = []
  const child = new EventEmitter()
  child.exitCode = null
  child.signalCode = null
  child.kill = signal => signals.push(signal)
  const error = await stopOwnedLauncher(child, milliseconds => {
    sleeps.push(milliseconds)
    return Promise.resolve()
  })
  assert.equal(error.message, 'owned emulator process did not exit after SIGKILL')
  assert.deepEqual(signals, ['SIGTERM', 'SIGKILL'])
  assert.deepEqual(sleeps, [1000, 5000, 5000])
})

it('does not signal unrelated emulator or adb processes from the session', async () => {
  const signals = []
  const runSyncCalls = []
  let launcherAlive = true
  const child = new EventEmitter()
  child.pid = 200
  child.exitCode = null
  child.signalCode = null
  child.kill = signal => {
    signals.push({ pid: child.pid, signal })
    launcherAlive = false
    child.exitCode = 0
    child.emit('exit', 0, signal)
  }
  let saved
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
    runCommand: (_program, args) => {
      if (args[0] === 'shell' && args[1] === 'getprop') {
        return {
          'sys.boot_completed': '1',
          'ro.build.version.sdk': '35',
          'ro.build.fingerprint': 'google/sdk_gphone64_x86_64/test-build',
          'ro.product.cpu.abilist': 'x86_64,arm64-v8a',
          'ro.dalvik.vm.native.bridge': 'libndk_translation.so',
        }[args[2]]
      }
      if (args[0] === 'shell' && args[1] === 'pm' && args[2] === 'list')
        return 'instrumentation:ai.voucha.android.test/androidx.test.runner.AndroidJUnitRunner (target=ai.voucha.android)'
      if (args[0] === 'shell' && args[1] === 'am' && args[2] === 'instrument') {
        const [className, methodName] = args[7].split('voucha.android.')[1].split('#')
        return [
          `INSTRUMENTATION_STATUS: class=voucha.android.${className}`,
          `INSTRUMENTATION_STATUS: test=${methodName}`,
          'INSTRUMENTATION_STATUS_CODE: 0',
          'OK (1 test)',
          'INSTRUMENTATION_CODE: -1',
        ].join('\n')
      }
      return ''
    },
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
    observeLineage: options =>
      selectOwnedLineage({
        ...options,
        records: ownedTree.filter(entry => entry.pid !== 200 || launcherAlive),
        scanComplete: true,
        limits: lineageLimits,
      }),
  })
  assert.deepEqual(signals, [{ pid: 200, signal: 'SIGTERM' }])
  assert.deepEqual(runSyncCalls, [{ program: 'adb', args: ['emu', 'kill'] }])
  const result = JSON.parse(saved)
  assert.equal(result.cleanup.avdName, avd)
  assert.deepEqual(
    result.cleanup.launch.processes.map(entry => entry.pid),
    [200, 201, 202],
  )
  assert.deepEqual(
    result.cleanup.shutdown.processes.map(entry => entry.pid),
    [201, 202],
  )
  assert.equal(JSON.stringify(result.cleanup).includes('paired-native-999'), false)
})

it('binds cleanup evidence to the run, attempt, profile, candidate, and runtime artifact', () => {
  const cleanup = cleanupFixture()
  assert.equal(validateCleanupEvidence(cleanup, cleanupBinding()), true)
  const unknownLaunch = cleanupFixture({
    launch: { state: 'unknown', launcherPid: 100, reason: 'truncated' },
  })
  assert.equal(validateCleanupEvidence(unknownLaunch, cleanupBinding()), true)
  assert.equal(Object.hasOwn(unknownLaunch.launch, 'processes'), false)
  for (const [binding, pattern] of [
    [cleanupBinding({ runId: '43' }), /another workflow run/u],
    [cleanupBinding({ runAttempt: 2 }), /another workflow run/u],
    [cleanupBinding({ nativeTestProfile: 'cookie-snapshot' }), /native test profile/u],
    [cleanupBinding({ clientsMergeSha: 'b'.repeat(40) }), /candidate merge SHA/u],
    [cleanupBinding({ runtimeArtifactId: 104 }), /runtime artifact ID/u],
    [cleanupBinding({ runtimeArtifactDigest: 'd'.repeat(64) }), /runtime artifact digest/u],
    [cleanupBinding({ runtimeArtifactId: 0 }), /runtime artifact ID/u],
    [cleanupBinding({ runtimeArtifactDigest: 'GG' }), /runtime artifact digest/u],
  ])
    assert.throws(() => validateCleanupEvidence(cleanup, binding), pattern)
  assert.throws(
    () =>
      validateCleanupEvidence(
        cleanupFixture({ launch: { state: 'clean', launcherPid: 100, processes: [] } }),
        cleanupBinding(),
      ),
    /malformed/u,
  )
  assert.throws(
    () =>
      validateCleanupEvidence(
        cleanupFixture({
          launch: {
            state: 'unknown',
            launcherPid: 100,
            reason: 'truncated',
            processes: [],
          },
        }),
        cleanupBinding(),
      ),
    /fields differ/u,
  )
})
