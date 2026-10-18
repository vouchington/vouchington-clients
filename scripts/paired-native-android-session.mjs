import { dirname, join } from 'node:path'
import { once } from 'node:events'
import {
  androidPhases,
  createInstrumentationResult,
  parseInstrumentationOutput,
} from './paired-native-test-evidence.mjs'

async function waitForBoot(runCommand, sleep) {
  runCommand('adb', ['wait-for-device'], { timeout: 120000 })
  for (let attempt = 0; attempt < 90; attempt += 1) {
    if (runCommand('adb', ['shell', 'getprop', 'sys.boot_completed'], { timeout: 10000 }) === '1')
      return
    await sleep(2000)
  }
  throw new Error('API 35 emulator did not finish booting')
}

export async function runAndroidSession({
  emulatorPath,
  avdName,
  runId,
  clientsMergeSha,
  apkSha256,
  appApk,
  appId,
  testId,
  emulatorVersion,
  logPath,
  resultPath,
  runCommand,
  runSync,
  launch,
  openLog,
  saveFile,
  makeDirectory,
  sleep,
}) {
  await makeDirectory(dirname(resultPath), { recursive: true })
  await makeDirectory(dirname(logPath), { recursive: true })
  const emulatorLog = openLog(logPath)
  try {
    if (!Number.isInteger(emulatorLog.fd)) await once(emulatorLog, 'open')
  } catch (error) {
    emulatorLog.destroy?.()
    throw error
  }
  const child = launch(
    emulatorPath,
    [
      '-avd',
      avdName,
      '-no-window',
      '-no-audio',
      '-no-boot-anim',
      '-gpu',
      'swiftshader_indirect',
      '-no-snapshot',
      '-accel',
      'on',
      '-memory',
      '2048',
      '-cores',
      '2',
    ],
    { stdio: ['ignore', emulatorLog, emulatorLog] },
  )
  if (!child.pid) {
    emulatorLog.end()
    throw new Error('could not start Android emulator')
  }
  let spawnError
  child.once('error', error => {
    spawnError = error
  })
  let executionError
  try {
    await waitForBoot(runCommand, sleep)
    if (spawnError || child.exitCode !== null)
      throw new Error(`emulator exited before tests: ${spawnError?.message ?? child.exitCode}`)
    const api = Number(
      runCommand('adb', ['shell', 'getprop', 'ro.build.version.sdk'], { timeout: 10000 }),
    )
    const fingerprint = runCommand('adb', ['shell', 'getprop', 'ro.build.fingerprint'], {
      timeout: 10000,
    })
    const abis = runCommand('adb', ['shell', 'getprop', 'ro.product.cpu.abilist'], {
      timeout: 10000,
    }).split(',')
    const nativeBridge = runCommand('adb', ['shell', 'getprop', 'ro.dalvik.vm.native.bridge'], {
      timeout: 10000,
    })
    if (
      api !== 35 ||
      !fingerprint ||
      !abis.includes('arm64-v8a') ||
      nativeBridge === '' ||
      nativeBridge === '0'
    )
      throw new Error('emulator lacks the required API 35 ARM64 native bridge')
    runCommand('adb', ['install', '-r', appApk], { timeout: 120000 })
    runCommand('adb', ['install', '-r', join(dirname(appApk), 'app-debug-androidTest.apk')], {
      timeout: 120000,
    })
    runCommand('adb', ['shell', 'pm', 'path', appId], { timeout: 15000 })
    const instruments = runCommand('adb', ['shell', 'pm', 'list', 'instrumentation'], {
      timeout: 15000,
    })
    if (
      !instruments.includes(`${testId}/androidx.test.runner.AndroidJUnitRunner (target=${appId})`)
    )
      throw new Error('instrumentation runner package does not target the reviewed application')
    const phases = []
    for (const method of androidPhases) {
      const [className, methodName] = method.split('#')
      const output = runCommand(
        'adb',
        [
          'shell',
          'am',
          'instrument',
          '-w',
          '-r',
          '-e',
          'class',
          `voucha.android.${className}#${methodName}`,
          `${testId}/androidx.test.runner.AndroidJUnitRunner`,
        ],
        { timeout: 180000 },
      )
      phases.push(parseInstrumentationOutput(output, method))
      runCommand('adb', ['shell', 'am', 'force-stop', appId], { timeout: 15000 })
    }
    const result = createInstrumentationResult({
      runId,
      clientsMergeSha,
      apkSha256,
      emulator: {
        version: emulatorVersion.replace(/^Android emulator version /u, ''),
        api,
        abis,
        nativeBridge,
        buildFingerprint: fingerprint,
      },
      phases,
    })
    await saveFile(resultPath, `${JSON.stringify(result, null, 2)}\n`, { flag: 'wx' })
  } catch (error) {
    executionError = error
  }
  try {
    runSync('adb', ['emu', 'kill'], { encoding: 'utf8', timeout: 15000, maxBuffer: 1024 * 1024 })
  } catch {
    /* fall back to the owned process handle */
  }
  const exited = async milliseconds => {
    if (child.exitCode !== null || child.signalCode != null) return true
    return Promise.race([
      new Promise(resolve => child.once('exit', () => resolve(true))),
      sleep(milliseconds).then(() => false),
    ])
  }
  let cleanupError
  if (!(await exited(1000))) {
    try {
      child.kill('SIGTERM')
    } catch {
      /* try SIGKILL below */
    }
    if (!(await exited(5000))) {
      try {
        child.kill('SIGKILL')
      } catch {
        /* report if the owned process remains alive */
      }
      if (!(await exited(5000)))
        cleanupError = new Error('owned emulator process did not exit after SIGKILL')
    }
  }
  emulatorLog.end()
  if (executionError) {
    if (cleanupError) executionError.message += `; ${cleanupError.message}`
    throw executionError
  }
  if (cleanupError) throw cleanupError
}
