import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import { chmodSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '..')
const run = (script, args = [], env = process.env) =>
  spawnSync('bash', [join(root, 'dev', script), ...args], {
    cwd: root,
    encoding: 'utf8',
    env,
  })

test('Linux setup requires an explicit producer checkout and rejects a nonempty stage', () => {
  const stage = mkdtempSync(join(tmpdir(), 'voucha-linux-stage-'))
  writeFileSync(join(stage, 'existing'), 'preserve')
  const missingProducer = run('setup-linux', ['--stage-root', stage], {
    ...process.env,
    VOUCHA_FILAMENTS_CONTRACT_ROOT: stage,
  })
  assert.equal(missingProducer.status, 2)
  assert.match(missingProducer.stderr, /--producer-root/)

  const nonempty = run('setup-linux', ['--producer-root', root, '--stage-root', stage])
  assert.equal(nonempty.status, 2)
  assert.match(nonempty.stderr, /empty directory/)
  assert.equal(readFileSync(join(stage, 'existing'), 'utf8'), 'preserve')
})

test('doctor and portable tests reject an implicit contract stage', () => {
  for (const script of ['linux-doctor', 'linux-portable-tests']) {
    const result = run(script, [], {
      ...process.env,
      VOUCHA_FILAMENTS_CONTRACT_ROOT: '/untrusted/ambient/path',
    })
    assert.equal(result.status, 2, script)
    assert.match(result.stderr, /--stage-root/, script)
  }
})

test('Linux image pins match the reviewed CI images', () => {
  const images = readFileSync(join(root, 'dev/linux-native-images.sh'), 'utf8')
  const native = readFileSync(join(root, '.github/workflows/native-contract-tests.yml'), 'utf8')
  const validate = readFileSync(join(root, '.github/workflows/validate.yml'), 'utf8')
  const entries = [[/SWIFT_LINUX_IMAGE='([^']+)'/, native]]
  for (const [pattern, workflow] of entries) {
    const image = images.match(pattern)?.[1]
    assert.ok(image, String(pattern))
    assert.ok(workflow.includes(image), image)
  }
  for (const pattern of [/SWIFTFORMAT_LINUX_IMAGE='([^']+)'/, /SWIFTLINT_LINUX_IMAGE='([^']+)'/]) {
    const image = images.match(pattern)?.[1]
    assert.match(image ?? '', /@sha256:[0-9a-f]{64}$/)
  }
  assert.match(validate, /bash swift-clients\/tooling\/harness\.sh --checks fmt,lint,lint-tests/)
})

test('the canonical Swift lint harness uses locked-down pinned containers on Linux', () => {
  const directory = mkdtempSync(join(tmpdir(), 'voucha-linux-lint-'))
  const bin = join(directory, 'bin')
  const log = join(directory, 'docker.log')
  mkdirSync(bin)
  const uname = join(bin, 'uname')
  const docker = join(bin, 'docker')
  writeFileSync(uname, '#!/bin/sh\nprintf "Linux\\n"\n')
  writeFileSync(docker, '#!/bin/sh\nprintf "%s\\n" "$*" >> "$DOCKER_LOG"\n')
  chmodSync(uname, 0o755)
  chmodSync(docker, 0o755)
  const result = spawnSync(
    'bash',
    [join(root, 'swift-clients/tooling/harness.sh'), '--checks', 'fmt,lint,lint-tests'],
    {
      cwd: root,
      encoding: 'utf8',
      env: { ...process.env, PATH: `${bin}:${process.env.PATH}`, DOCKER_LOG: log },
    },
  )
  assert.equal(result.status, 0, result.stdout + result.stderr)
  const calls = readFileSync(log, 'utf8').trim().split('\n')
  assert.equal(calls.length, 3)
  for (const call of calls) {
    assert.match(call, /--read-only --network none/)
    assert.match(call, /--volume .*:\/workspace:ro/)
    assert.match(call, /--env HOME=\/tmp/)
  }
  assert.match(calls[0], /swiftformat:0\.61\.1@sha256:/)
  assert.match(calls[1], /swiftlint:0\.65\.0@sha256:/)
  assert.match(calls[2], /swiftlint:0\.65\.0@sha256:/)
})

test('Linux scripts parse as Bash', () => {
  for (const script of [
    'linux-native-images.sh',
    'linux-doctor',
    'setup-linux',
    'linux-portable-tests',
    'linux-quality',
  ]) {
    execFileSync('bash', ['-n', join(root, 'dev', script)])
  }
})
