import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { chmod, copyFile, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const sourceRoot = new URL('../swift-clients/apps/android/tooling/', import.meta.url)

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'voucha-android-phases-'))
  const tooling = join(root, 'tooling')
  const bin = join(root, '.build/artifacts/skip/skip/skip.artifactbundle/bin')
  await mkdir(tooling, { recursive: true })
  await mkdir(bin, { recursive: true })
  for (const name of [
    'pre-push.sh',
    'link-swift64-android-test-runner.sh',
    'build-native-product.sh',
  ]) {
    await copyFile(new URL(name, sourceRoot), join(tooling, name))
  }
  const module = join(
    root,
    '.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid',
  )
  await mkdir(join(module, 'src/main/swift'), { recursive: true })
  await writeFile(join(module, 'src/main/swift/Package.swift'), '// generated fixture')
  await copyFile(
    new URL('fixtures/android-native-gradle-task.gradle.kts', import.meta.url),
    join(module, 'build.gradle.kts'),
  )
  const trace = join(root, 'trace')
  await writeFile(trace, '')
  await writeFile(join(bin, 'java'), '#!/bin/sh\nexit 0\n')
  await writeFile(
    join(bin, 'skip'),
    `#!/bin/bash
set -eu
printf '%s\\n' "$*" >> "$PHASE_TRACE"
if [[ "$1" == plugin ]]; then
  if [[ "\${FAIL_PLUGIN:-0}" != 0 ]]; then exit "$FAIL_PLUGIN"; fi
  while [[ "$1" != --plugin-ref ]]; do shift; done
  plugin_ref="$2"
  plugin_path="$PHASE_PACKAGE/.build/Android/skip-gradle"
  generated="$PHASE_PACKAGE/.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid/src/main/swift"
  mkdir -p "$plugin_path" "$generated"
  printf '%s' "$plugin_path" > "$plugin_ref"
  printf '// generated fixture\\n' > "$generated/Package.swift"
  exit 0
fi
if [[ "$2" == build ]]; then
  if [[ "\${FAIL_BUILD:-0}" != 0 ]]; then exit "$FAIL_BUILD"; fi
  products="$PHASE_PACKAGE/.build/out/Products/Debug-android-aarch64"
  mkdir -p "$products"
  printf '#!/bin/sh\\nexit 0\\n' > "$products/VouchaAndroidTests-test-runner"
  chmod +x "$products/VouchaAndroidTests-test-runner"
else
  test -x "$PHASE_PACKAGE/.build/out/Products/Debug-android-aarch64/voucha-androidPackageTests.xctest"
  exit "\${FAIL_LIBS:-0}"
fi
`,
  )
  for (const name of ['skip', 'java']) await chmod(join(bin, name), 0o755)
  const env = {
    ...process.env,
    PATH: `${bin}:${process.env.PATH}`,
    VOUCHA_SKIP_ANDROID_HOST_SWIFT_TEST: '1',
    PHASE_TRACE: trace,
    PHASE_PACKAGE: root,
  }
  delete env.VOUCHA_SKIP_SWIFT_HOME
  delete env.GITHUB_STEP_SUMMARY
  return {
    root,
    env,
    async gradle() {
      await mkdir(join(root, 'Android'))
      await writeFile(
        join(root, 'Android/gradlew'),
        `#!/bin/sh
printf 'gradle %s\\n' "$*" >> "$PHASE_TRACE"
if [ "\${NATIVE_REUSED:-0}" = 1 ]; then
  printf '%s\\n' '> Task :skipstone:VouchaAndroid:buildAndroidSwiftPackageDebug UP-TO-DATE'
fi
exit "\${FAIL_APP:-0}"
`,
      )
      await chmod(join(root, 'Android/gradlew'), 0o755)
    },
    run(args, overrides = {}) {
      return spawnSync('bash', [join(tooling, 'pre-push.sh'), ...args], {
        env: { ...env, ...overrides },
        encoding: 'utf8',
        timeout: 10_000,
      })
    },
    lines: async () => (await readFile(trace, 'utf8')).trim().split('\n').filter(Boolean),
    clean: () => rm(root, { recursive: true, force: true }),
  }
}

test('direct Swift phases never probe Java or Homebrew', async () => {
  const f = await fixture()
  try {
    const bin = join(f.root, '.build/artifacts/skip/skip/skip.artifactbundle/bin')
    for (const command of ['java', 'brew']) {
      await writeFile(
        join(bin, command),
        '#!/bin/sh\nprintf "unexpected Java probe\\n" >&2\nexit 99\n',
      )
      await chmod(join(bin, command), 0o755)
    }
    for (const phase of ['app-native-dependency', 'app-native-producer']) {
      const result = f.run(['--phase', phase], { RUNNER_TEMP: f.root })
      assert.equal(result.status, 0, result.stderr)
      assert.doesNotMatch(result.stderr, /unexpected Java probe/u)
    }
  } finally {
    await f.clean()
  }
})

test('APK phase needs no Android test runner and never packages test libraries', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    const result = f.run(['--phase', 'app'])
    assert.equal(result.status, 0, result.stderr)
    assert.deepEqual(await f.lines(), [
      'gradle --profile :app:assembleDebug :app:assembleDebugAndroidTest',
    ])
  } finally {
    await f.clean()
  }
})

test('a missing profile summary cannot change the Gradle build result', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    const summary = join(f.root, 'summary.md')
    const success = f.run(['--phase', 'app'], { GITHUB_STEP_SUMMARY: summary })
    assert.equal(success.status, 0, success.stderr)
    assert.match(success.stderr, /unable to summarize Android Gradle profile/u)
    assert.match(await readFile(summary, 'utf8'), /Android Gradle timing/u)
    const failure = f.run(['--phase', 'app'], { GITHUB_STEP_SUMMARY: summary, FAIL_APP: '42' })
    assert.equal(failure.status, 42)
  } finally {
    await f.clean()
  }
})

test('test-library phase succeeds in a fresh workspace with no Gradle project output', async () => {
  const f = await fixture()
  try {
    const result = f.run(['--phase', 'test-libs'])
    assert.equal(result.status, 0, result.stderr)
    const lines = await f.lines()
    assert.equal(lines.length, 2)
    assert.match(lines[0], /^android build .* --arch aarch64 --build-tests$/u)
    assert.match(lines[1], /^android test .* --build-test-libs /u)
  } finally {
    await f.clean()
  }
})

test('native producer builds its product directly and consumer requires verified state', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    assert.equal(f.run(['--phase', 'app-native-producer']).status, 0)
    const lines = await f.lines()
    assert.equal(lines.length, 1)
    assert.match(lines[0], /android build .* --product VouchaAndroid /u)
    assert.doesNotMatch(lines[0], /gradle/u)
    assert.equal(f.run(['--phase', 'app-consumer']).status, 1)
    const verified = { VOUCHA_ANDROID_STATE_VERIFIED: '1', RUNNER_TEMP: f.root }
    assert.equal(f.run(['--phase', 'app-consumer'], verified).status, 0)
    const reused = f.run(['--phase', 'app-consumer'], { ...verified, NATIVE_REUSED: '1' })
    assert.equal(reused.status, 0)
    assert.match(reused.stdout, /buildAndroidSwiftPackageDebug UP-TO-DATE/u)
    assert.equal(f.run(['--phase', 'app-consumer'], { ...verified, FAIL_APP: '13' }).status, 13)
  } finally {
    await f.clean()
  }
})

test('native producer retains Skip source hashes only after verified state restore', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    const hashes = join(
      f.root,
      '.build/plugins/outputs/skip-fuse-ui/SkipSwiftUI/destination/skipstone',
    )
    const marker = join(hashes, '.SkipSwiftUI.sourcehash')
    await mkdir(hashes, { recursive: true })
    await writeFile(marker, 'restored dependency marker')
    assert.equal(f.run(['--phase', 'app-native-producer']).status, 0)
    await assert.rejects(readFile(marker, 'utf8'), { code: 'ENOENT' })

    await writeFile(marker, 'restored dependency marker')
    assert.equal(
      f.run(['--phase', 'app-native-producer'], { VOUCHA_ANDROID_STATE_VERIFIED: 'bogus' }).status,
      0,
    )
    await assert.rejects(readFile(marker, 'utf8'), { code: 'ENOENT' })

    await writeFile(marker, 'restored dependency marker')
    assert.equal(
      f.run(['--phase', 'app-native-producer'], { VOUCHA_ANDROID_STATE_VERIFIED: '1' }).status,
      0,
    )
    assert.equal(await readFile(marker, 'utf8'), 'restored dependency marker')

    assert.equal(f.run(['--phase', 'app'], { VOUCHA_ANDROID_STATE_VERIFIED: '1' }).status, 0)
    await assert.rejects(readFile(marker, 'utf8'), { code: 'ENOENT' })
  } finally {
    await f.clean()
  }
})

test('native producer opts in to restored Skip markers only after exact-run verification', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const native = workflow
    .split('  build-swift-android-native:\n')[1]
    .split('  build-swift-android:\n')[0]
  const restore = native.indexOf(
    'restore candidate-clients/swift-clients/apps/android "$RUNNER_TEMP/android-dependency-state"',
  )
  const verified = native.indexOf('echo \'VOUCHA_ANDROID_STATE_VERIFIED=1\' >> "$GITHUB_ENV"')
  const build = native.indexOf('ANDROID_BUILD_PHASE: app-native-producer')
  assert.ok(restore >= 0 && verified > restore && build > verified)
  assert.match(native, /name: Download exact-run native dependency state/u)
})

test('native dependency phase uses the pinned prebuild to generate the Gradle package', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    const result = f.run(['--phase', 'app-native-dependency'], { RUNNER_TEMP: f.root })
    assert.equal(result.status, 0, result.stderr)
    const lines = await f.lines()
    assert.equal(lines.length, 2)
    assert.match(lines[0], /^plugin --prebuild --package-path .* --plugin-ref /u)
    assert.match(
      lines[1],
      /android build .* --package-path .*\/src\/main\/swift --configuration debug --target SkipSwiftUI --scratch-path .*\/build\/swift --arch automatic /u,
    )
    assert.doesNotMatch(lines.join('\n'), /gradle /u)
    assert.equal(
      f.run(['--phase', 'app-native-dependency'], { RUNNER_TEMP: f.root, FAIL_PLUGIN: '37' })
        .status,
      37,
    )
  } finally {
    await f.clean()
  }
})

test('local default retains both phases and every failed phase fails validation', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    assert.equal(f.run([]).status, 0)
    assert.equal((await f.lines()).length, 3)
    assert.equal(f.run(['--phase', 'app'], { FAIL_APP: '42' }).status, 42)
    assert.equal(f.run(['--phase', 'test-libs'], { FAIL_BUILD: '43' }).status, 43)
    assert.equal(f.run(['--phase', 'test-libs'], { FAIL_LIBS: '44' }).status, 44)
  } finally {
    await f.clean()
  }
})

test('invalid phase options fail before touching build state or launching commands', async () => {
  const f = await fixture()
  try {
    const hashes = join(f.root, '.build/plugins/outputs')
    await mkdir(hashes, { recursive: true })
    await writeFile(join(hashes, '.kept.sourcehash'), 'keep')
    for (const args of [
      ['app'],
      ['--phase'],
      ['--phase', 'unknown'],
      ['--phase', 'app', 'extra'],
    ]) {
      assert.equal(f.run(args).status, 64)
    }
    assert.deepEqual(await f.lines(), [])
    assert.equal(await readFile(join(hashes, '.kept.sourcehash'), 'utf8'), 'keep')
  } finally {
    await f.clean()
  }
})

test('stable Android fan-in rejects every failed, cancelled or skipped phase', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  for (const job of [
    'build-swift-android-dependency',
    'build-swift-android-native',
    'build-swift-android',
    'test-swift-android-libs',
    'test-swift-android-host',
  ]) {
    const block = workflow.split(`  ${job}:\n`)[1].split(/^  [a-z][^\n]*:\n/mu)[0]
    assert.match(block, /runs-on: macos-latest\n    timeout-minutes: (?:20|30)\n/u)
  }
  const gate = workflow
    .split('  test-swift-android-cookie:\n')[1]
    .split('  build-swift-ui-tests:\n')[0]
  assert.match(gate, /name: Swift Android Skip/u)
  assert.match(
    gate,
    /needs:\n      \[\n        verify,\n        build-swift-android-dependency,\n        build-swift-android-native,\n        build-swift-android,\n        test-swift-android-libs,\n        test-swift-android-host,\n      \]/u,
  )
  assert.match(gate, /if: \$\{\{ !cancelled\(\) \}\}/u)
  const run = gate.match(/ {8}run: \|\n((?: {10}[^\n]*\n)+)/u)
  assert.ok(run)
  const script = run[1].replace(/^ {10}/gmu, '')
  const names = [
    'VERIFY_RESULT',
    'ANDROID_DEPENDENCY_RESULT',
    'ANDROID_NATIVE_RESULT',
    'ANDROID_APP_RESULT',
    'ANDROID_LIBS_RESULT',
    'ANDROID_HOST_RESULT',
  ]
  for (const name of names) {
    for (const outcome of ['failure', 'cancelled', 'skipped']) {
      const env = Object.fromEntries(names.map(key => [key, key === name ? outcome : 'success']))
      const result = spawnSync('bash', ['-e', '-c', script], { env: { ...process.env, ...env } })
      assert.notEqual(result.status, 0, `${name}=${outcome} was accepted`)
    }
  }
  const result = spawnSync('bash', ['-e', '-c', script], {
    env: { ...process.env, ...Object.fromEntries(names.map(name => [name, 'success'])) },
  })
  assert.equal(result.status, 0)
  assert.ok(
    gate.indexOf('Require every Android validation phase') <
      gate.indexOf('Download exact candidate Android APKs'),
  )
  assert.match(gate, /Reject invalid Android artifact and instrumentation results/u)
  assert.match(gate, /Prove checked encrypted storage survives Android process restarts/u)
  const tests = workflow.split('  tests:\n')[1]
  assert.match(tests, /^      - test-swift-android-cookie$/mu)
  assert.doesNotMatch(workflow, /^  test-swift-android:\n/mu)
})
