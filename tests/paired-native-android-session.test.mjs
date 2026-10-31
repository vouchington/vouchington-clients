import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { EventEmitter, once } from 'node:events'
import { createWriteStream } from 'node:fs'
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { androidPhases } from '../scripts/paired-native-test-profiles.mjs'
import { runAndroidSession } from '../scripts/paired-native-android-session.mjs'
import { passingAdbCommand } from './paired-native-adb-command.mjs'

it('opens the emulator log before attaching it to a real child process', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-session-spawn-'))
  const logPath = join(root, 'logs', 'emulator.log')
  const resultPath = join(root, 'results', 'runtime-result.json')
  let logStream
  try {
    await runAndroidSession({
      emulatorPath: process.execPath,
      avdName: 'paired-native-1234',
      runId: '1234',
      nativeTestProfile: 'pending-turn-recovery',
      clientsMergeSha: 'a'.repeat(40),
      apkSha256: 'b'.repeat(64),
      appApk: join(root, 'app-debug.apk'),
      appId: 'ai.voucha.android',
      testId: 'ai.voucha.android.test',
      emulatorVersion: 'Android emulator version 37.2.12.0 (build_id 16428233)',
      logPath,
      resultPath,
      runCommand: passingAdbCommand,
      runSync: () => ({ status: 0, stdout: '', stderr: '' }),
      launch: (_program, _args, options) => {
        assert.ok(Number.isInteger(options.stdio[1].fd), 'log stream must be open before spawn')
        return spawn(
          process.execPath,
          ['-e', "process.stdout.write('child-log-ready\\n')"],
          options,
        )
      },
      openLog: path => {
        logStream = createWriteStream(path)
        return logStream
      },
      saveFile: writeFile,
      makeDirectory: mkdir,
      sleep: milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds)),
      observeLineage: async ({ launcherPid, claimAbsent }) =>
        claimAbsent
          ? { state: 'observed', launcherPid, processes: [] }
          : {
              state: 'observed',
              launcherPid,
              processes: [
                {
                  pid: launcherPid,
                  ppid: 1,
                  pgid: launcherPid,
                  command: 'emulator -avd paired-native-1234',
                },
              ],
            },
    })

    await once(logStream, 'finish')
    assert.match(await readFile(logPath, 'utf8'), /child-log-ready/u)
    const result = JSON.parse(await readFile(resultPath, 'utf8'))
    assert.deepEqual(
      result.phases.map(phase => phase.method),
      androidPhases,
    )
    assert.equal(result.cleanup.avdName, 'paired-native-1234')
    assert.equal(result.cleanup.shutdown.state, 'observed')
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('does not spawn when opening the emulator log fails', async () => {
  const logStream = new EventEmitter()
  let destroyed = false
  let launched = false
  logStream.destroy = () => {
    destroyed = true
  }
  await assert.rejects(
    runAndroidSession({
      logPath: '/tmp/paired-emulator.log',
      resultPath: '/tmp/paired-runtime.json',
      makeDirectory: async () => {},
      openLog: () => {
        queueMicrotask(() => logStream.emit('error', new Error('log open failed')))
        return logStream
      },
      launch: () => {
        launched = true
      },
    }),
    /log open failed/u,
  )
  assert.equal(destroyed, true)
  assert.equal(launched, false)
})
