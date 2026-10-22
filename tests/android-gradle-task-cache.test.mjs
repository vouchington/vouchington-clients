import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  realpathSync,
  rmSync,
  statSync,
  symlinkSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { inspectGradleBuildCache } from '../swift-clients/apps/android/tooling/check-gradle-build-cache.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const pathHelper = join(
  repositoryRoot,
  'swift-clients/apps/android/tooling/gradle-build-cache-dir.sh',
)
const cacheCheck = join(
  repositoryRoot,
  'swift-clients/apps/android/tooling/check-gradle-build-cache.mjs',
)

test('workflow and build select the same real Darwin task-output cache path', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'android-gradle-cache-path-'))
  try {
    const fakeBin = join(fixture, 'bin')
    const parent = join(fixture, 'darwin-temp')
    mkdirSync(fakeBin)
    mkdirSync(parent)
    writeFileSync(join(fakeBin, 'uname'), '#!/bin/sh\nprintf "Darwin\\n"\n', { mode: 0o755 })
    writeFileSync(join(fakeBin, 'getconf'), '#!/bin/sh\nprintf "%s\\n" "$CACHE_TEST_PARENT"\n', {
      mode: 0o755,
    })
    const env = {
      ...process.env,
      GITHUB_ACTIONS: 'false',
      PATH: `${fakeBin}:${process.env.PATH}`,
      CACHE_TEST_PARENT: parent,
    }
    const selected = spawnSync('bash', [pathHelper], { env, encoding: 'utf8' })
    assert.equal(selected.status, 0, selected.stderr)
    const cacheDir = join(realpathSync(parent), 'gradle-build-cache')
    assert.equal(selected.stdout.trim(), cacheDir)
    assert.equal(statSync(cacheDir).mode & 0o777, 0o700)

    const selectedAgain = spawnSync('bash', [pathHelper], { env, encoding: 'utf8' })
    assert.equal(selectedAgain.status, 0, selectedAgain.stderr)
    assert.equal(selectedAgain.stdout.trim(), cacheDir)

    rmSync(cacheDir, { recursive: true })
    symlinkSync(fixture, cacheDir)
    const unsafe = spawnSync('bash', [pathHelper], { env, encoding: 'utf8' })
    assert.notEqual(unsafe.status, 0)
    assert.match(unsafe.stderr, /must not be a symlink/u)

    const runnerHome = join(fixture, 'runner-home')
    mkdirSync(runnerHome)
    const ciEnv = { ...env, GITHUB_ACTIONS: 'true', HOME: runnerHome }
    const selectedCi = spawnSync('bash', [pathHelper], { env: ciEnv, encoding: 'utf8' })
    assert.equal(selectedCi.status, 0, selectedCi.stderr)
    const ciCacheDir = join(realpathSync(runnerHome), '.cache/voucha/gradle-build-cache')
    assert.equal(selectedCi.stdout.trim(), ciCacheDir)
    assert.equal(statSync(ciCacheDir).mode & 0o777, 0o700)

    rmSync(join(runnerHome, '.cache/voucha'), { recursive: true })
    symlinkSync(fixture, join(runnerHome, '.cache/voucha'))
    const unsafeCi = spawnSync('bash', [pathHelper], { env: ciEnv, encoding: 'utf8' })
    assert.notEqual(unsafeCi.status, 0)
    assert.match(unsafeCi.stderr, /cache parent must be a real directory/u)
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})

test('task-output cache save scan bounds logical bytes, members, and time', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'android-gradle-cache-scan-'))
  try {
    const cacheDir = join(fixture, 'gradle-build-cache')
    mkdirSync(cacheDir)
    mkdirSync(join(cacheDir, 'nested'))
    writeFileSync(join(cacheDir, 'nested', 'result.bin'), 'abcd')
    assert.deepEqual(inspectGradleBuildCache(cacheDir), {
      eligible: true,
      reason: 'within-bounds',
      entries: 2,
      bytes: 4,
    })
    assert.equal(
      inspectGradleBuildCache(cacheDir, { bytes: 3, entries: 10, scanMs: 1000 }).reason,
      'logical-byte-size',
    )
    assert.equal(
      inspectGradleBuildCache(cacheDir, { bytes: 100, entries: 1, scanMs: 1000 }).reason,
      'member-count',
    )
    assert.equal(
      inspectGradleBuildCache(cacheDir, { bytes: 100, entries: 10, scanMs: -1 }).reason,
      'scan-time',
    )

    const output = join(fixture, 'step-output')
    writeFileSync(output, '')
    const cli = spawnSync(process.execPath, [cacheCheck, cacheDir, output], { encoding: 'utf8' })
    assert.equal(cli.status, 0, cli.stderr)
    assert.equal(readFileSync(output, 'utf8'), 'save=true\n')

    symlinkSync(join(cacheDir, 'nested'), join(cacheDir, 'link'))
    assert.throws(() => inspectGradleBuildCache(cacheDir), /unsafe member/u)
    const unsafeCli = spawnSync(process.execPath, [cacheCheck, cacheDir, output], {
      encoding: 'utf8',
    })
    assert.notEqual(unsafeCli.status, 0)
    assert.equal(
      readFileSync(output, 'utf8'),
      'save=true\n',
      'unsafe cache must not write another save decision',
    )
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
