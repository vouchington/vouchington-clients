import { dirname, join } from 'node:path'
import { once } from 'node:events'
import { androidPhasesForProfile } from './paired-native-test-profiles.mjs'
import { createInstrumentationResult } from './paired-native-test-runtime-result.mjs'
import { parseInstrumentationOutput } from './paired-native-test-evidence.mjs'
import {
  assertOwnedAvd,
  captureLineage,
  sessionCleanup,
  stopOwnedLauncher,
  waitForEmulatorBoot,
} from './paired-native-emulator-lineage.mjs'
import { observeOwnedEmulatorLineage } from './paired-native-proc-snapshot.mjs'

export async function runAndroidSession({
  emulatorPath,
  avdName,
  runId,
  nativeTestProfile,
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
  observeLineage = observeOwnedEmulatorLineage,
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
  try {
    assertOwnedAvd(runId, avdName)
  } catch (error) {
    emulatorLog.end()
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
  const launchLineage = await captureLineage(observeLineage, { launcherPid: child.pid, avdName })
  let phases
  let api
  let fingerprint
  let abis
  let nativeBridge
  let executionError
  try {
    await waitForEmulatorBoot(runCommand, sleep)
    if (spawnError || child.exitCode !== null)
      throw new Error(`emulator exited before tests: ${spawnError?.message ?? child.exitCode}`)
    api = Number(
      runCommand('adb', ['shell', 'getprop', 'ro.build.version.sdk'], { timeout: 10000 }),
    )
    fingerprint = runCommand('adb', ['shell', 'getprop', 'ro.build.fingerprint'], {
      timeout: 10000,
    })
    abis = runCommand('adb', ['shell', 'getprop', 'ro.product.cpu.abilist'], {
      timeout: 10000,
    }).split(',')
    nativeBridge = runCommand('adb', ['shell', 'getprop', 'ro.dalvik.vm.native.bridge'], {
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
    phases = []
    for (const method of androidPhasesForProfile(nativeTestProfile)) {
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
  } catch (error) {
    executionError = error
  }
  const ownedBeforeStop = await captureLineage(observeLineage, {
    launcherPid: child.pid,
    avdName,
  })
  try {
    runSync('adb', ['emu', 'kill'], { encoding: 'utf8', timeout: 15000, maxBuffer: 1024 * 1024 })
  } catch {
    /* fall back to the owned process handle */
  }
  const cleanupError = await stopOwnedLauncher(child, sleep)
  const shutdownLineage = await captureLineage(observeLineage, {
    launcherPid: child.pid,
    avdName,
    priorIdentities: ownedBeforeStop.identities ?? [],
    claimAbsent: ownedBeforeStop.state === 'observed',
  })
  try {
    if (!executionError && !cleanupError) {
      const result = createInstrumentationResult({
        runId,
        nativeTestProfile,
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
        cleanup: sessionCleanup({
          runId,
          nativeTestProfile,
          clientsMergeSha,
          avdName,
          launch: launchLineage,
          shutdown: shutdownLineage,
        }),
      })
      await saveFile(resultPath, `${JSON.stringify(result, null, 2)}\n`, { flag: 'wx' })
    }
  } finally {
    emulatorLog.end()
  }
  if (executionError) {
    if (cleanupError) executionError.message += `; ${cleanupError.message}`
    throw executionError
  }
  if (cleanupError) throw cleanupError
}
