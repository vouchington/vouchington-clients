import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { EventEmitter, once } from 'node:events'
import { createWriteStream } from 'node:fs'
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { androidPhases } from '../scripts/paired-native-test-evidence.mjs'
import { runAndroidSession } from '../scripts/paired-native-android-session.mjs'

it('opens the emulator log before attaching it to a real child process', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-session-spawn-'))
  const logPath = join(root, 'logs', 'emulator.log')
  const resultPath = join(root, 'results', 'runtime-result.json')
  let logStream
  try {
    const runCommand = (_program, args) => {
      if (args[0] === 'shell' && args[1] === 'getprop') {
        return (
          {
            'sys.boot_completed': '1',
            'ro.build.version.sdk': '35',
            'ro.build.fingerprint': 'google/sdk_gphone64_x86_64/test-build',
            'ro.product.cpu.abilist': 'x86_64,arm64-v8a',
            'ro.dalvik.vm.native.bridge': 'libndk_translation.so',
          }[args[2]] ?? ''
        )
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
    }

    await runAndroidSession({
      emulatorPath: process.execPath,
      avdName: 'paired-native-test',
      runId: '1234',
      clientsMergeSha: 'a'.repeat(40),
      apkSha256: 'b'.repeat(64),
      appApk: join(root, 'app-debug.apk'),
      appId: 'ai.voucha.android',
      testId: 'ai.voucha.android.test',
      emulatorVersion: 'Android emulator version 37.2.12.0 (build_id 16428233)',
      logPath,
      resultPath,
      runCommand,
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
    })

    await once(logStream, 'finish')
    assert.match(await readFile(logPath, 'utf8'), /child-log-ready/u)
    const result = JSON.parse(await readFile(resultPath, 'utf8'))
    assert.deepEqual(
      result.phases.map(phase => phase.method),
      androidPhases,
    )
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
