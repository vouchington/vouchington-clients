import assert from 'node:assert/strict'
import { EventEmitter } from 'node:events'
import { mkdir, mkdtemp, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import {
  androidPhases,
  bindArtifact,
  createInstrumentationResult,
  parseInstrumentationOutput,
  sha256,
  validateInstrumentationResult,
  validateNativeAbiEntries,
  verifyApkBundle,
} from '../scripts/paired-native-test-evidence.mjs'
import { runPairedAndroidEmulator } from '../scripts/paired-native-android-emulator.mjs'

it('binds an artifact to exact action metadata and the current run', () => {
  const digest = 'a'.repeat(64)
  const expected = { id: 42, digest, name: 'paired-native-apks-1-1' }
  const artifact = {
    ...expected,
    digest: `sha256:${digest}`,
    expired: false,
    workflow_run: { id: 77 },
  }
  assert.equal(
    bindArtifact({
      expected,
      runId: '77',
      runAttempt: 1,
      artifact,
    }),
    true,
  )
  for (const mutate of [
    value => ({ ...value, id: 43 }),
    value => ({ ...value, digest: `sha256:${'b'.repeat(64)}` }),
    value => ({ ...value, digest: `sha512:${digest}` }),
    value => ({ ...value, digest: 'sha256:not-a-digest' }),
    value => ({ ...value, expired: true }),
    value => ({ ...value, workflow_run: { id: 78 } }),
  ]) {
    assert.throws(() =>
      bindArtifact({ expected, runId: '77', runAttempt: 1, artifact: mutate(artifact) }),
    )
  }
  for (const badDigest of ['sha256:abc', 'A'.repeat(64), `${digest}:suffix`])
    assert.throws(() =>
      bindArtifact({
        expected: { ...expected, digest: badDigest },
        runId: '77',
        runAttempt: 1,
        artifact,
      }),
    )
})

it('accepts only the fixed APK pair, reviewed revision, and matching checksums', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-apks-'))
  try {
    const app = Buffer.from('candidate app bytes')
    const tests = Buffer.from('candidate test bytes')
    await writeFile(join(root, 'app-debug.apk'), app)
    await writeFile(join(root, 'app-debug-androidTest.apk'), tests)
    await writeFile(join(root, 'candidate-revision.txt'), `${'a'.repeat(40)}\n`)
    await writeFile(
      join(root, 'sha256sums.txt'),
      `${sha256(app)}  app-debug.apk\n${sha256(tests)}  app-debug-androidTest.apk\n`,
    )
    assert.deepEqual(await verifyApkBundle(root, 'a'.repeat(40)), {
      'app-debug.apk': sha256(app),
      'app-debug-androidTest.apk': sha256(tests),
    })
    await assert.rejects(verifyApkBundle(root, 'b'.repeat(40)), /revision/u)
    await writeFile(join(root, 'extra.txt'), 'unexpected')
    await assert.rejects(verifyApkBundle(root, 'a'.repeat(40)), /allowlist|file-tree limit/u)
    await rm(join(root, 'extra.txt'))
    await mkdir(join(root, 'nested'))
    await writeFile(join(root, 'nested', 'inside'), 'unexpected')
    await assert.rejects(verifyApkBundle(root, 'a'.repeat(40)), /file-tree limit/u)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

it('rejects symlinked APK artifacts and malformed instrumentation output', async () => {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-apks-link-'))
  const outside = await mkdtemp(join(tmpdir(), 'paired-native-apks-outside-'))
  try {
    await writeFile(join(outside, 'external'), 'x')
    await symlink(join(outside, 'external'), join(root, 'app-debug.apk'))
    await writeFile(join(root, 'app-debug-androidTest.apk'), 'test')
    await writeFile(join(root, 'candidate-revision.txt'), `${'a'.repeat(40)}\n`)
    await writeFile(join(root, 'sha256sums.txt'), 'invalid')
    await assert.rejects(verifyApkBundle(root, 'a'.repeat(40)), /symlink/u)
  } finally {
    await rm(root, { recursive: true, force: true })
    await rm(outside, { recursive: true, force: true })
  }
  const success =
    'INSTRUMENTATION_STATUS: class=voucha.android.CookieSnapshotBridgeRestartTest\nINSTRUMENTATION_STATUS: test=seedCheckedEntries\nINSTRUMENTATION_STATUS_CODE: 0\nOK (1 test)\nINSTRUMENTATION_CODE: -1\n'
  assert.deepEqual(parseInstrumentationOutput(success, androidPhases[0]), {
    method: androidPhases[0],
    exitCode: 0,
    tests: 1,
  })
  assert.throws(
    () =>
      parseInstrumentationOutput(
        'Test results for InstrumentationTestRunner\nTime: 0.1\nOK (1 test)\n',
        androidPhases[0],
      ),
    /expected method/u,
  )
  assert.throws(
    () =>
      parseInstrumentationOutput(
        success.replace('INSTRUMENTATION_CODE: -1', 'INSTRUMENTATION_CODE: 0'),
        androidPhases[0],
      ),
    /failed/u,
  )
  assert.throws(
    () =>
      parseInstrumentationOutput(
        success.replace('test=seedCheckedEntries', 'test=other'),
        androidPhases[0],
      ),
    /expected method/u,
  )
})

it('requires every fixed restart phase exactly once and rejects source or run rebinding', () => {
  const emulator = {
    version: '37.2.12.0',
    api: 35,
    abis: ['x86_64', 'arm64-v8a'],
    nativeBridge: 'libndk_translation.so',
    buildFingerprint:
      'google/sdk_gphone64_x86_64/emu64x:15/AB1A.240505.005/1234567:userdebug/test-keys',
  }
  const value = {
    schema: 1,
    runId: '42',
    runAttempt: 1,
    clientsMergeSha: 'a'.repeat(40),
    apkSha256: 'b'.repeat(64),
    emulator,
    phases: androidPhases.map(method => ({ method, exitCode: 0, tests: 1 })),
  }
  assert.equal(
    validateInstrumentationResult(value, {
      runId: '42',
      clientsMergeSha: 'a'.repeat(40),
      apkSha256: 'b'.repeat(64),
    }),
    true,
  )
  assert.throws(
    () =>
      validateInstrumentationResult(
        { ...value, phases: value.phases.slice(1) },
        {
          runId: '42',
          clientsMergeSha: 'a'.repeat(40),
          apkSha256: 'b'.repeat(64),
        },
      ),
    /incomplete/u,
  )
  assert.throws(
    () =>
      validateInstrumentationResult(value, {
        runId: '43',
        clientsMergeSha: 'a'.repeat(40),
        apkSha256: 'b'.repeat(64),
      }),
    /another workflow run/u,
  )
  assert.throws(
    () =>
      validateInstrumentationResult(value, {
        runId: '42',
        clientsMergeSha: 'c'.repeat(40),
        apkSha256: 'b'.repeat(64),
      }),
    /mismatched/u,
  )
  assert.throws(
    () =>
      validateInstrumentationResult(
        { ...value, emulator: { ...emulator, version: 37 } },
        {
          runId: '42',
          clientsMergeSha: 'a'.repeat(40),
          apkSha256: 'b'.repeat(64),
        },
      ),
    /emulator/u,
  )
})

it('accepts only the three required ARM64 JNI libraries and no alternate ABI', () => {
  const required = [
    'lib/arm64-v8a/libSkipAndroidBridge.so',
    'lib/arm64-v8a/libSwiftJNI.so',
    'lib/arm64-v8a/libVouchaAndroid.so',
  ]
  assert.equal(validateNativeAbiEntries(required), true)
  assert.throws(() => validateNativeAbiEntries(required.slice(1)), /libSkipAndroidBridge/u)
  assert.throws(
    () => validateNativeAbiEntries([...required, 'lib/x86_64/libVouchaAndroid.so']),
    /unsupported native ABI/u,
  )
})

function instrumentationOutput(method, { missingResult = false, failure = false } = {}) {
  const [className, methodName] = method.split('#')
  return [
    `INSTRUMENTATION_STATUS: class=voucha.android.${className}`,
    `INSTRUMENTATION_STATUS: test=${methodName}`,
    'INSTRUMENTATION_STATUS_CODE: 0',
    failure ? 'FAILURES!!!' : 'OK (1 test)',
    missingResult ? '' : 'INSTRUMENTATION_CODE: -1',
  ]
    .filter(Boolean)
    .join('\n')
}

async function runDriver({
  wrongPackage = false,
  failedMethod,
  missingMethod,
  emulatorVersion = 'Android emulator version 37.2.12.0 (build_id 16428233) (CL:N/A)',
  outsideEmulatorPath = false,
  symlinkedEmulator = false,
} = {}) {
  const root = await mkdtemp(join(tmpdir(), 'paired-native-driver-'))
  const apkRoot = join(root, 'apks')
  const emulatorRoot = join(root, 'paired-pinned-emulator', 'emulator')
  const emulatorPath = join(emulatorRoot, 'emulator')
  await mkdir(apkRoot)
  await mkdir(emulatorRoot, { recursive: true })
  const app = Buffer.from('verified app apk bytes')
  const testApk = Buffer.from('verified instrumentation apk bytes')
  await writeFile(join(apkRoot, 'app-debug.apk'), app)
  await writeFile(join(apkRoot, 'app-debug-androidTest.apk'), testApk)
  await writeFile(join(apkRoot, 'candidate-revision.txt'), `${'a'.repeat(40)}\n`)
  await writeFile(
    join(apkRoot, 'sha256sums.txt'),
    `${sha256(app)}  app-debug.apk\n${sha256(testApk)}  app-debug-androidTest.apk\n`,
  )
  if (symlinkedEmulator) {
    const targetPath = join(root, 'external-emulator')
    await writeFile(targetPath, 'pinned emulator bytes', { mode: 0o755 })
    await symlink(targetPath, emulatorPath)
  } else {
    await writeFile(emulatorPath, 'pinned emulator bytes', { mode: 0o755 })
  }
  const commands = []
  const invocations = []
  const versionProbes = []
  const results = new Map()
  let child
  const runCommand = (program, args) => {
    commands.push(args)
    invocations.push({ program, args })
    if (program === 'unzip')
      return [
        'lib/arm64-v8a/libSkipAndroidBridge.so',
        'lib/arm64-v8a/libSwiftJNI.so',
        'lib/arm64-v8a/libVouchaAndroid.so',
      ].join('\n')
    if (program.endsWith('/aapt'))
      return `package: name='${args.at(-1).endsWith('androidTest.apk') ? (wrongPackage ? 'bad.test' : 'ai.voucha.android.test') : 'ai.voucha.android'}'`
    if (args[0] === 'shell' && args[1] === 'getprop') {
      return (
        {
          'sys.boot_completed': '1',
          'ro.build.version.sdk': '35',
          'ro.build.fingerprint':
            'google/sdk_gphone64_x86_64/emu64x:15/AB1A.240505.005/1234567:userdebug/test-keys',
          'ro.product.cpu.abilist': 'x86_64,arm64-v8a',
          'ro.dalvik.vm.native.bridge': 'libndk_translation.so',
        }[args[2]] ?? ''
      )
    }
    if (args[0] === 'shell' && args[1] === 'pm' && args[2] === 'list')
      return 'instrumentation:ai.voucha.android.test/androidx.test.runner.AndroidJUnitRunner (target=ai.voucha.android)'
    if (args[0] === 'shell' && args[1] === 'am' && args[2] === 'instrument') {
      const requested = args[7].split('voucha.android.')[1]
      return instrumentationOutput(requested, {
        failure: requested === failedMethod,
        missingResult: requested === missingMethod,
      })
    }
    return ''
  }
  const runSync = (program, args) => {
    if (program.endsWith('/emulator') && args[0] === '-version') {
      versionProbes.push(program)
      return { status: 0, stdout: '', stderr: `${emulatorVersion}\n` }
    }
    return { status: 0, stdout: '', stderr: '' }
  }
  const dependencies = {
    env: {
      APK_BUNDLE_ROOT: apkRoot,
      ANDROID_HOME: '/android-sdk',
      RUNNER_TEMP: root,
      PINNED_EMULATOR_PATH: outsideEmulatorPath ? join(root, 'external-emulator') : emulatorPath,
      CLIENTS_MERGE_SHA: 'a'.repeat(40),
      GITHUB_RUN_ID: '7654',
      GITHUB_RUN_ATTEMPT: '1',
      RUNTIME_RESULT_PATH: join(root, 'runtime', 'runtime-result.json'),
      EMULATOR_LOG_PATH: join(root, 'logs', 'emulator.log'),
    },
    uid: 1000,
    runCommand,
    runSync,
    access: async () => {},
    mkdir: async () => {},
    writeFile: async (path, contents) => results.set(path, contents),
    createWriteStream: () => ({ fd: 1, end() {} }),
    spawn: () => {
      child = new EventEmitter()
      child.pid = 123
      child.exitCode = null
      child.signalCode = null
      child.kill = () => {
        child.exitCode = 0
        child.emit('exit', 0)
      }
      return child
    },
    sleep: async () => {},
  }
  try {
    await runPairedAndroidEmulator(dependencies)
    return { root, commands, invocations, versionProbes, results, child, emulatorPath }
  } catch (error) {
    return { root, commands, invocations, versionProbes, results, child, error, emulatorPath }
  }
}

it('runs the fixed eight Android phases, force-stops between them, and emits validator-ready evidence', async () => {
  const execution = await runDriver()
  try {
    assert.equal(execution.error, undefined)
    assert.ok(
      execution.invocations.some(
        ({ program, args }) => program === 'sdkmanager' && !args.includes('emulator'),
      ),
      'SDK manager must resolve from setup-android PATH and not install over the pinned emulator',
    )
    assert.ok(
      execution.invocations.some(
        ({ program, args }) => program === 'avdmanager' && args[0] === 'create',
      ),
      'AVD manager must resolve from setup-android PATH',
    )
    assert.deepEqual(execution.versionProbes, [execution.emulatorPath])
    const instrumentCalls = execution.commands.filter(
      args => args[0] === 'shell' && args[1] === 'am' && args[2] === 'instrument',
    )
    const stopCalls = execution.commands.filter(
      args => args[0] === 'shell' && args[1] === 'am' && args[2] === 'force-stop',
    )
    assert.ok(
      instrumentCalls.every(args => args.includes('-r')),
      'instrumentation must use raw status output',
    )
    assert.deepEqual(
      instrumentCalls.map(args => args[7].split('voucha.android.')[1]),
      androidPhases,
    )
    assert.equal(stopCalls.length, androidPhases.length)
    assert.deepEqual(
      execution.commands
        .filter(
          args =>
            args[0] === 'shell' &&
            args[1] === 'am' &&
            ['instrument', 'force-stop'].includes(args[2]),
        )
        .map(args =>
          args[2] === 'instrument'
            ? `instrument:${args[7].split('voucha.android.')[1]}`
            : 'force-stop',
        ),
      androidPhases.flatMap(method => [`instrument:${method}`, 'force-stop']),
    )
    const json = [...execution.results.values()].map(value => JSON.parse(value)).at(0)
    assert.equal(
      createInstrumentationResult({
        runId: json.runId,
        clientsMergeSha: json.clientsMergeSha,
        apkSha256: json.apkSha256,
        emulator: json.emulator,
        phases: json.phases,
      }).runId,
      '7654',
    )
    assert.equal(
      validateInstrumentationResult(json, {
        runId: '7654',
        clientsMergeSha: 'a'.repeat(40),
        apkSha256: sha256(Buffer.from('verified app apk bytes')),
      }),
      true,
    )
  } finally {
    await rm(execution.root, { recursive: true, force: true })
  }
})

it('rejects wrong APK package identity and failed or incomplete instrumentation while cleaning the child', async () => {
  for (const options of [
    { wrongPackage: true },
    { failedMethod: androidPhases[2] },
    { missingMethod: androidPhases[4] },
    { emulatorVersion: 'Android emulator version 37.2.13.0 (build_id 16428233) (CL:N/A)' },
    { outsideEmulatorPath: true },
    { symlinkedEmulator: true },
  ]) {
    const execution = await runDriver(options)
    try {
      assert.ok(execution.error)
      assert.equal(execution.results.size, 0)
      assert.ok(
        execution.child === undefined || execution.child.exitCode === 0,
        JSON.stringify({ options, exitCode: execution.child?.exitCode }),
      )
    } finally {
      await rm(execution.root, { recursive: true, force: true })
    }
  }
})
