import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { runAndroidSession } from '../scripts/paired-native-android-session.mjs'
import { validateCleanupEvidence } from '../scripts/paired-native-cleanup-evidence.mjs'
import { lineageLimits, ownedAvdName } from '../scripts/paired-native-emulator-lineage.mjs'
import { selectOwnedLineage } from '../scripts/paired-native-ownership.mjs'
import { observeOwnedEmulatorLineage } from '../scripts/paired-native-proc-snapshot.mjs'
import { readBoundedFile } from '../scripts/paired-native-proc-read.mjs'
import { cleanupBinding, cleanupFixture } from './paired-native-cleanup-fixture.mjs'

const avd = ownedAvdName('55')

function record(pid, ppid, pgid, command, startTime) {
  return { pid, ppid, pgid, startTime, identity: { command } }
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
