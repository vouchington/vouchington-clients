import { spawn, spawnSync } from 'node:child_process'
import { access, lstat, mkdir, writeFile } from 'node:fs/promises'
import { constants, createWriteStream } from 'node:fs'
import { join, resolve } from 'node:path'
import { androidPhasesForProfile } from './paired-native-test-profiles.mjs'
import { validateNativeAbiEntries, verifyApkBundle } from './paired-native-test-evidence.mjs'
import { ownedAvdName } from './paired-native-emulator-lineage.mjs'
import { runAndroidSession } from './paired-native-android-session.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'

const fail = prefixedFailure('Paired Android runtime evidence failed')

function command(program, args, options = {}, runSync = spawnSync) {
  const result = runSync(program, args, {
    encoding: 'utf8',
    timeout: 180000,
    maxBuffer: 8 * 1024 * 1024,
    ...options,
  })
  if (result.error || result.status !== 0)
    fail(
      `${program} ${args.join(' ')} failed: ${(result.stderr || result.error?.message || '').slice(-4000)}`,
    )
  return result.stdout.replaceAll('\r', '').trim()
}

function packageId(apkTool, apk, runCommand) {
  const output = runCommand(apkTool, ['dump', 'badging', apk], { timeout: 30000 })
  return /^package: name='([^']+)'/mu.exec(output)?.[1]
}

function verifyArm64Libraries(apk, runCommand) {
  const entries = runCommand('unzip', ['-Z1', apk], { timeout: 30000 }).split('\n')
  validateNativeAbiEntries(entries)
}

async function verifyPinnedEmulatorPath(runnerTemp, requestedPath) {
  if (typeof runnerTemp !== 'string' || runnerTemp.length === 0)
    fail('runner temporary directory is missing')
  const root = resolve(runnerTemp)
  const expected = join(root, 'paired-pinned-emulator', 'emulator', 'emulator')
  if (typeof requestedPath !== 'string' || resolve(requestedPath) !== expected)
    fail('pinned emulator path is outside the job-owned emulator directory')
  const packageRoot = join(root, 'paired-pinned-emulator')
  const emulatorDirectory = join(packageRoot, 'emulator')
  const [rootInfo, directoryInfo, executableInfo] = await Promise.all([
    lstat(packageRoot),
    lstat(emulatorDirectory),
    lstat(expected),
  ])
  const runnerUid = process.getuid?.()
  if (
    runnerUid === undefined ||
    rootInfo.isSymbolicLink() ||
    !rootInfo.isDirectory() ||
    rootInfo.uid !== runnerUid ||
    directoryInfo.isSymbolicLink() ||
    !directoryInfo.isDirectory() ||
    directoryInfo.uid !== runnerUid ||
    executableInfo.isSymbolicLink() ||
    !executableInfo.isFile() ||
    executableInfo.uid !== runnerUid ||
    (executableInfo.mode & 0o111) === 0
  )
    fail('pinned emulator is not a job-owned executable file')
  return expected
}

export async function runPairedAndroidEmulator(dependencies = {}) {
  const env = dependencies.env ?? process.env
  const uid = dependencies.uid ?? process.getuid?.()
  const runSync = dependencies.runSync ?? spawnSync
  const launch = dependencies.spawn ?? spawn
  const checkAccess = dependencies.access ?? access
  const makeDirectory = dependencies.mkdir ?? mkdir
  const saveFile = dependencies.writeFile ?? writeFile
  const openLog = dependencies.createWriteStream ?? createWriteStream
  const sleep =
    dependencies.sleep ??
    (milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds)))
  const runCommand =
    dependencies.runCommand ??
    ((program, args, options = {}) => command(program, args, options, runSync))
  if (uid === undefined || uid === 0) fail('emulator runner must not run as root')
  const apkRoot = env.APK_BUNDLE_ROOT
  const resultPath = env.RUNTIME_RESULT_PATH
  const logPath = env.EMULATOR_LOG_PATH
  const clientsMergeSha = env.CLIENTS_MERGE_SHA
  const nativeTestProfile = env.NATIVE_TEST_PROFILE
  const runId = env.GITHUB_RUN_ID
  const runAttempt = Number(env.GITHUB_RUN_ATTEMPT)
  if (
    ![apkRoot, resultPath, logPath, clientsMergeSha, runId, nativeTestProfile].every(
      value => typeof value === 'string' && value.length > 0,
    )
  )
    fail('required runtime inputs are missing')
  if (runAttempt !== 1) fail('rerun attempts are not accepted')
  androidPhasesForProfile(nativeTestProfile)
  const sdkRoot = env.ANDROID_HOME ?? env.ANDROID_SDK_ROOT
  if (!sdkRoot) fail('Android SDK root is missing')
  const emulatorPath = await verifyPinnedEmulatorPath(env.RUNNER_TEMP, env.PINNED_EMULATOR_PATH)
  const apkDigests = await verifyApkBundle(apkRoot, clientsMergeSha)
  const appApk = join(apkRoot, 'app-debug.apk')
  const testApk = join(apkRoot, 'app-debug-androidTest.apk')
  verifyArm64Libraries(appApk, runCommand)
  const apkTool = join(sdkRoot, 'build-tools', '36.0.0', 'aapt')
  const appId = packageId(apkTool, appApk, runCommand)
  const testId = packageId(apkTool, testApk, runCommand)
  if (appId !== 'ai.voucha.android' || testId !== 'ai.voucha.android.test')
    fail('APK package identity differs from the fixed Android application')
  try {
    await checkAccess('/dev/kvm', constants.R_OK | constants.W_OK)
  } catch {
    fail('KVM is unavailable to the emulator runner')
  }
  const sdkManager = 'sdkmanager'
  runCommand(
    sdkManager,
    [
      '--install',
      'platform-tools',
      'platforms;android-35',
      'system-images;android-35;google_apis;x86_64',
    ],
    { timeout: 600000 },
  )
  const avdName = ownedAvdName(runId)
  runCommand(
    'avdmanager',
    [
      'create',
      'avd',
      '--force',
      '-n',
      avdName,
      '-k',
      'system-images;android-35;google_apis;x86_64',
    ],
    { input: 'no\n', timeout: 120000 },
  )
  const versionProbe = runSync(emulatorPath, ['-version'], {
    encoding: 'utf8',
    timeout: 15000,
    maxBuffer: 1024 * 1024,
  })
  if (versionProbe.error || versionProbe.status !== 0)
    fail(
      `emulator version probe failed: ${(versionProbe.stderr || versionProbe.error?.message || '').slice(-4000)}`,
    )
  const emulatorVersion = `${versionProbe.stdout ?? ''}\n${versionProbe.stderr ?? ''}`
    .trim()
    .split('\n')[0]
  if (
    !/^Android emulator version 37\.2\.12\.0 \(build_id 16428233\)(?: \(CL:N\/A\))?$/u.test(
      emulatorVersion,
    )
  )
    fail(`unexpected emulator version: ${emulatorVersion}`)
  await runAndroidSession({
    emulatorPath,
    avdName,
    runId,
    nativeTestProfile,
    clientsMergeSha,
    apkSha256: apkDigests['app-debug.apk'],
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
    observeLineage: dependencies.observeLineage,
  })
}

if (import.meta.main) await runPairedAndroidEmulator()
