import assert from 'node:assert/strict'
import { spawn, spawnSync } from 'node:child_process'
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import test from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const entrypoint = join(
  repositoryRoot,
  'swift-clients/apps/android/tooling/run-android-host-swift-tests.sh',
)
const fullTargetCommand =
  'bash swift-clients/apps/android/tooling/run-android-host-swift-tests.sh --package-path swift-clients/apps/android --filter VouchaAndroidTests'

async function executable(path, contents) {
  await writeFile(path, `#!/bin/bash\nset -euo pipefail\n${contents}\n`)
  await chmod(path, 0o755)
}

async function fixture(t, { xcode = '26.6', swift = '6.3.3' } = {}) {
  const root = await mkdtemp(join(tmpdir(), 'voucha-android-host-entrypoint-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const bin = join(root, 'bin')
  const developerDir = join(root, 'Xcode.app', 'Contents', 'Developer')
  const swiftPath = join(developerDir, 'Toolchains/XcodeDefault.xctoolchain/usr/bin/swift')
  const logPath = join(root, 'invocations.log')
  await mkdir(bin, { recursive: true })
  await mkdir(dirname(swiftPath), { recursive: true })
  await mkdir(join(developerDir, 'usr/bin'), { recursive: true })
  await writeFile(logPath, '')
  await executable(join(bin, 'uname'), "printf 'Darwin\\n'")
  await executable(
    join(bin, 'xcrun'),
    `[[ "$1" == '--find' && "$2" == 'swift' ]] || exit 2\nprintf '%s\\n' '${swiftPath}'`,
  )
  await executable(
    join(developerDir, 'usr/bin/xcodebuild'),
    `printf 'Xcode ${xcode}\\nBuild version test\\n'`,
  )
  await executable(
    swiftPath,
    `if [[ "\${1:-}" == test ]]; then printf 'swift test\\n' >> '${logPath}'; exit 99; fi\nprintf 'Swift version ${swift} (swift-${swift}-RELEASE)\\n'`,
  )
  await executable(
    join(bin, 'mise'),
    `printf '%s\\n' "$*" >> '${logPath}'
if [[ "$1" == install && "$2" == swift@6.3.3 ]]; then exit 0; fi
if [[ "$*" == 'exec swift@6.3.3 -- swift --version' ]]; then printf 'Swift version 6.3.3 (swift-6.3.3-RELEASE)\\n'; exit 0; fi
if [[ "$1" == exec && "$4" == swift && "$5" == test ]]; then printf 'auto=%s\\n' "\${MISE_EXEC_AUTO_INSTALL:-}" >> '${logPath}'; exit 0; fi
exit 2`,
  )
  return {
    developerDir,
    logPath,
    run(args = []) {
      return new Promise((resolvePromise, reject) => {
        const child = spawn('bash', [entrypoint, ...args], {
          env: {
            ...process.env,
            DEVELOPER_DIR: developerDir,
            PATH: `${bin}:${process.env.PATH}`,
          },
        })
        let stdout = ''
        let stderr = ''
        child.stdout.setEncoding('utf8').on('data', chunk => (stdout += chunk))
        child.stderr.setEncoding('utf8').on('data', chunk => (stderr += chunk))
        child.on('error', reject)
        child.on('close', code => resolvePromise({ code, stdout, stderr }))
      })
    },
  }
}

test('android host entrypoint fails on Xcode 27.1 before SwiftPM', async t => {
  const f = await fixture(t, { xcode: '27.1', swift: '6.4.0' })
  const result = await f.run([
    '--package-path',
    'swift-clients/apps/android',
    '--filter',
    'VouchaAndroidTests',
  ])
  assert.equal(result.code, 1)
  assert.match(result.stderr, /require selected Xcode 26\.6; found 27\.1/u)
  assert.equal(await readFile(f.logPath, 'utf8'), '')
})

test('android host entrypoint provisions Swift 6.3.3 and forwards planner arguments', async t => {
  const f = await fixture(t)
  const result = await f.run([
    '--package-path',
    'swift-clients/apps/android',
    '--filter',
    'VouchaAndroidTests',
  ])
  assert.equal(result.code, 0, result.stderr)
  const log = await readFile(f.logPath, 'utf8')
  assert.match(log, /^install swift@6\.3\.3$/mu)
  assert.match(
    log,
    /exec swift@6\.3\.3 -- swift test --force-resolved-versions --disable-dependency-cache --disable-keychain --disable-netrc --package-path swift-clients\/apps\/android --filter VouchaAndroidTests/u,
  )
  assert.match(log, /^auto=false$/mu)
  assert.doesNotMatch(log, /^swift test$/mu)
})

test('planner, host-lock harness, and Android CI share the host entrypoint', async () => {
  const config = await readFile(join(repositoryRoot, '.no-mistakes.yml'), 'utf8')
  const harness = await readFile(join(repositoryRoot, 'swift-clients/tooling/harness.sh'), 'utf8')
  const workflow = await readFile(
    join(repositoryRoot, '.github/workflows/native-contract-tests.yml'),
    'utf8',
  )
  const workspace = await readFile(join(repositoryRoot, 'pnpm-workspace.yaml'), 'utf8')
  assert.match(
    config,
    /baseCommands:\n {6}swift-clients\/apps\/android:\n {8}- bash\n {8}- swift-clients\/apps\/android\/tooling\/run-android-host-swift-tests\.sh/u,
  )
  assert.match(workspace, /minimumReleaseAgeExclude:[\s\S]*- no-mistakes/u)
  assert.match(config, /- name: no-mistakes/u)
  assert.match(harness, /test-android-host/u)
  assert.match(
    harness,
    /with-build-lock\.sh" \\\n\s+bash swift-clients\/apps\/android\/tooling\/run-android-host-swift-tests\.sh \\\n\s+--package-path swift-clients\/apps\/android \\\n\s+--filter VouchaAndroidTests/u,
  )
  assert.match(
    workflow,
    new RegExp(fullTargetCommand.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&').replace(/ /gu, '\\s+')),
  )
  assert.doesNotMatch(workflow, /with-build-lock|with-host-lock/u)

  const planned = spawnSync(
    'pnpm',
    [
      'exec',
      'no-mistakes',
      'tests',
      'plan',
      'swift',
      '--changed-file',
      'swift-clients/apps/android/Tests/VouchaAndroidTests/ViewModelTests.swift',
      '--format',
      'commands',
      '--lock-timeout',
      '300',
    ],
    { cwd: repositoryRoot, encoding: 'utf8' },
  )
  assert.equal(planned.status, 0, planned.stderr)
  const commands = planned.stdout.trim().split('\n')
  const androidCommands = commands.filter(command => command.includes('swift-clients/apps/android'))
  assert.deepEqual(androidCommands, [fullTargetCommand])
  assert.ok(
    commands.some(command => command.startsWith('swift test --package-path swift-clients/')),
  )
  assert.equal(
    commands
      .filter(command => !command.includes('swift-clients/apps/android'))
      .every(command => command.startsWith('swift test ')),
    true,
  )
})
