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
  for (const name of ['pre-push.sh', 'link-swift64-android-test-runner.sh']) {
    await copyFile(new URL(name, sourceRoot), join(tooling, name))
  }
  const trace = join(root, 'trace')
  await writeFile(trace, '')
  await writeFile(join(bin, 'java'), '#!/bin/sh\nexit 0\n')
  await writeFile(
    join(bin, 'skip'),
    `#!/bin/bash
set -eu
printf '%s\\n' "$*" >> "$PHASE_TRACE"
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

test('APK phase needs no Android test runner and never packages test libraries', async () => {
  const f = await fixture()
  try {
    await f.gradle()
    const result = f.run(['--phase', 'app'])
    assert.equal(result.status, 0, result.stderr)
    assert.deepEqual(await f.lines(), ['gradle :app:assembleDebug'])
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

test('stable Android fan-in rejects failed, cancelled or skipped phase and verify results', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const matrixJob = workflow
    .split('  build-swift-android:\n')[1]
    .split('  test-swift-android:\n')[0]
  assert.match(matrixJob, /fail-fast: false/u)
  assert.match(matrixJob, /phase: \[app, test-libs\]/u)
  const gate = workflow.split('  test-swift-android:\n')[1].split('  test-swift-ui:\n')[0]
  assert.match(gate, /name: Swift Android Skip/u)
  assert.match(gate, /needs: \[verify, build-swift-android\]/u)
  assert.match(gate, /if: \$\{\{ !cancelled\(\) \}\}/u)
  const run = gate.match(/ {8}run: \|\n((?: {10}[^\n]*\n)+)/u)
  assert.ok(run)
  const script = run[1].replace(/^ {10}/gmu, '')
  for (const verify of ['success', 'failure', 'cancelled', 'skipped']) {
    for (const phases of ['success', 'failure', 'cancelled', 'skipped']) {
      const result = spawnSync('bash', ['-e', '-c', script], {
        env: { ...process.env, VERIFY_RESULT: verify, ANDROID_PHASES_RESULT: phases },
      })
      assert.equal(result.status === 0, verify === 'success' && phases === 'success')
    }
  }
})
