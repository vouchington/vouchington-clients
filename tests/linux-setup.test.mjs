import assert from 'node:assert/strict'
import { execFileSync, spawnSync } from 'node:child_process'
import {
  chmodSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  symlinkSync,
  writeFileSync,
} from 'node:fs'
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

test('Linux setup accepts equivalent explicit Git root spellings', () => {
  const directory = mkdtempSync(join(tmpdir(), 'voucha-linux-root-'))
  const producer = join(directory, 'producer')
  const stage = join(directory, 'stage')
  const bin = join(directory, 'bin')
  mkdirSync(join(producer, 'api-fixtures/v1'), { recursive: true })
  mkdirSync(join(producer, 'dev'))
  mkdirSync(stage)
  mkdirSync(bin)
  writeFileSync(join(producer, 'dev/native-localization.mts'), '')
  execFileSync('git', ['init', '-q', producer])
  writeFileSync(join(bin, 'uname'), '#!/bin/sh\nprintf "Darwin\\n"\n')
  chmodSync(join(bin, 'uname'), 0o755)
  for (const spelling of [`${producer}/`, `${producer}/.`]) {
    const result = run('setup-linux', ['--producer-root', spelling, '--stage-root', stage], {
      ...process.env,
      PATH: `${bin}:${process.env.PATH}`,
    })
    assert.equal(result.status, 1, result.stdout + result.stderr)
    assert.match(result.stderr, /Linux doctor requires a Linux host/u)
    assert.doesNotMatch(result.stderr, /--producer-root must be the root/u)
  }
})

test('Linux setup rejects a stage beneath a symlinked source ancestor', () => {
  const directory = mkdtempSync(join(tmpdir(), 'voucha-linux-stage-link-'))
  const producer = join(directory, 'producer')
  const link = join(directory, 'source-link')
  mkdirSync(join(producer, 'api-fixtures/v1'), { recursive: true })
  mkdirSync(join(producer, 'dev'))
  writeFileSync(join(producer, 'dev/native-localization.mts'), '')
  execFileSync('git', ['init', '-q', producer])
  symlinkSync(producer, link, 'dir')
  const result = run('setup-linux', [
    '--producer-root',
    producer,
    '--stage-root',
    join(link, 'stage'),
  ])
  assert.equal(result.status, 2, result.stdout + result.stderr)
  assert.match(result.stderr, /outside the client and producer checkouts/u)
  assert.equal(readFileSync(join(producer, 'dev/native-localization.mts'), 'utf8'), '')
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
  assert.equal(run('linux-doctor', ['--setup-ready']).status, 2)
})

test('setup-ready requires checkout dependencies, restore assets, and pinned images', t => {
  const checkout = mkdtempSync(join(tmpdir(), 'voucha-linux-ready-'))
  t.after(() => rmSync(checkout, { recursive: true, force: true }))
  const bin = join(checkout, 'bin')
  const stage = join(checkout, 'stage')
  mkdirSync(join(checkout, 'dev'))
  mkdirSync(bin)
  mkdirSync(stage)
  writeFileSync(join(checkout, 'dev/linux-doctor'), readFileSync(join(root, 'dev/linux-doctor')))
  writeFileSync(
    join(checkout, 'dev/linux-native-images.sh'),
    readFileSync(join(root, 'dev/linux-native-images.sh')),
  )
  writeFileSync(join(checkout, 'global.json'), '{"sdk":{"version":"10.0.301"}}')
  writeFileSync(
    join(checkout, 'package.json'),
    JSON.stringify({
      devDependencies: { 'vouchington-tooling': '1', 'no-mistakes': '1', oxfmt: '1' },
    }),
  )
  writeFileSync(join(checkout, 'pnpm-lock.yaml'), 'pinned-lock')
  execFileSync('git', ['init', '-q', checkout])
  execFileSync('git', ['-C', checkout, 'add', 'global.json', 'package.json', 'pnpm-lock.yaml'])
  execFileSync('git', [
    '-C',
    checkout,
    '-c',
    'user.name=Setup Test',
    '-c',
    'user.email=setup-test@example.invalid',
    'commit',
    '-qm',
    'initial setup inputs',
  ])
  for (const [name, body] of [
    ['uname', 'printf "Linux\\n"'],
    ['pnpm', 'if [[ "$1" == --version ]]; then echo 12.0.0; fi'],
    ['dotnet', 'echo 10.0.301'],
    [
      'docker',
      'if [[ "$1" == info ]]; then echo linux; elif [[ -n "${FAIL_IMAGE:-}" ]]; then exit 1; fi',
    ],
    ['mise', 'echo "{}"'],
  ]) {
    const path = join(bin, name)
    writeFileSync(path, `#!/bin/bash\n${body}\n`)
    chmodSync(path, 0o755)
  }
  const environment = { ...process.env, PATH: `${bin}:${process.env.PATH}` }
  const doctor = env =>
    spawnSync(
      'bash',
      [join(checkout, 'dev/linux-doctor'), '--stage-root', stage, '--setup-ready'],
      {
        cwd: checkout,
        encoding: 'utf8',
        env,
      },
    )
  const missing = doctor(environment)
  assert.equal(missing.status, 1, missing.stdout + missing.stderr)
  assert.match(missing.stderr, /Checkout dependencies are absent/u)

  mkdirSync(join(checkout, 'node_modules/.bin'), { recursive: true })
  mkdirSync(join(checkout, 'node_modules/.pnpm'))
  for (const [name, bin] of [
    ['vouchington-tooling', 'vouchington'],
    ['no-mistakes', 'no-mistakes'],
    ['oxfmt', 'oxfmt'],
  ]) {
    const packageRoot = join(checkout, 'node_modules', name)
    mkdirSync(packageRoot, { recursive: true })
    writeFileSync(
      join(packageRoot, 'package.json'),
      JSON.stringify({ name, bin: { [bin]: 'cli.js' } }),
    )
  }
  for (const name of ['vouchington', 'no-mistakes', 'oxfmt']) {
    const path = join(checkout, 'node_modules/.bin', name)
    writeFileSync(path, '#!/bin/sh\nexit 0\n')
    chmodSync(path, 0o755)
  }
  writeFileSync(join(checkout, 'node_modules/.pnpm/lock.yaml'), 'pinned-lock')
  rmSync(join(checkout, 'node_modules/.bin/oxfmt'))
  const missingFormatter = doctor(environment)
  assert.equal(missingFormatter.status, 1, missingFormatter.stdout + missingFormatter.stderr)
  assert.match(missingFormatter.stderr, /Checkout dependencies are absent/u)
  writeFileSync(join(checkout, 'node_modules/.bin/oxfmt'), '#!/bin/sh\nexit 0\n')
  chmodSync(join(checkout, 'node_modules/.bin/oxfmt'), 0o755)
  for (const project of ['src/Voucha.Client.Core', 'tests/Voucha.Client.Core.Tests']) {
    const directory = join(checkout, 'dotnet-clients', project, 'obj')
    mkdirSync(directory, { recursive: true })
    writeFileSync(join(directory, 'project.assets.json'), '{}')
  }
  const missingImage = doctor({ ...environment, FAIL_IMAGE: '1' })
  assert.equal(missingImage.status, 1, missingImage.stdout + missingImage.stderr)
  assert.match(missingImage.stderr, /Pinned Linux image is missing/u)
  const ready = doctor(environment)
  assert.equal(ready.status, 0, ready.stdout + ready.stderr)
  writeFileSync(join(checkout, 'global.json'), '{"sdk":{"version":"10.0.301"}}\n')
  const dirty = doctor(environment)
  assert.equal(dirty.status, 1, dirty.stdout + dirty.stderr)
  assert.match(dirty.stderr, /restore inputs changed locally/u)
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
