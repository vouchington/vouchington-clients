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
import { pathToFileURL } from 'node:url'
import { inspectGradleBuildCache } from '../swift-clients/apps/android/tooling/check-gradle-build-cache.mjs'
import { selectGradleTaskCache } from '../swift-clients/apps/android/tooling/select-gradle-task-cache.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const pathHelper = join(
  repositoryRoot,
  'swift-clients/apps/android/tooling/gradle-build-cache-dir.sh',
)
const cacheCheck = join(
  repositoryRoot,
  'swift-clients/apps/android/tooling/check-gradle-build-cache.mjs',
)
const stateCli = join(repositoryRoot, 'swift-clients/apps/android/tooling/android-state-cli.mjs')

const revisionA = 'a'.repeat(40)
const revisionB = 'b'.repeat(40)
const revisionC = 'c'.repeat(40)
const baseCacheEvent = {
  RUNNER_OS: 'macOS',
  RUNNER_ARCH: 'ARM64',
  TASK_CACHE_CONFIG_HASH: 'f'.repeat(64),
  CANDIDATE_REVISION: revisionB,
  PREVIOUS_MAIN_REVISION: revisionA,
  GITHUB_EVENT_NAME: 'push',
  GITHUB_REF: 'refs/heads/main',
}

test('revision-chain keys seed each successful main source revision without prefix lookup', () => {
  const path = '/Users/runner/.cache/voucha/gradle-build-cache'
  const main = selectGradleTaskCache(baseCacheEvent, path)
  assert.match(main.cacheKey, new RegExp(`-${revisionB}$`, 'u'))
  assert.equal(main.previousKey, main.cacheKey.replace(revisionB, revisionA))
  assert.notEqual(
    selectGradleTaskCache({ ...baseCacheEvent, CANDIDATE_REVISION: revisionC }, path).cacheKey,
    main.cacheKey,
    'a new main source revision must receive a new immutable save key',
  )
  const pr = selectGradleTaskCache(
    {
      ...baseCacheEvent,
      GITHUB_EVENT_NAME: 'pull_request',
      GITHUB_REF: 'refs/pull/123/merge',
      PR_BASE_REVISION: revisionA,
      CANDIDATE_REVISION: revisionC,
    },
    path,
  )
  assert.equal(pr.cacheKey, main.previousKey)
  assert.equal(pr.previousKey, undefined, 'a pull request must not restore a predecessor or save')
  assert.notEqual(
    selectGradleTaskCache({ ...baseCacheEvent, RUNNER_ARCH: 'X64' }, path).cacheKey,
    main.cacheKey,
  )
  assert.notEqual(
    selectGradleTaskCache({ ...baseCacheEvent, TASK_CACHE_CONFIG_HASH: 'e'.repeat(64) }, path)
      .cacheKey,
    main.cacheKey,
  )
  assert.notEqual(selectGradleTaskCache(baseCacheEvent, `${path}-other`).cacheKey, main.cacheKey)
})

test('task-cache key selection fails closed for malformed events and zero predecessor', () => {
  const path = '/Users/runner/.cache/voucha/gradle-build-cache'
  assert.equal(
    selectGradleTaskCache({ ...baseCacheEvent, PREVIOUS_MAIN_REVISION: '0'.repeat(40) }, path)
      .previousKey,
    undefined,
    'a new main branch has no predecessor cache to restore',
  )
  assert.throws(
    () => selectGradleTaskCache({ ...baseCacheEvent, PREVIOUS_MAIN_REVISION: '' }, path),
    /previous revision/u,
  )
  assert.throws(
    () =>
      selectGradleTaskCache({ ...baseCacheEvent, GITHUB_EVENT_NAME: 'workflow_dispatch' }, path),
    /only accepts/u,
  )
  assert.throws(
    () => selectGradleTaskCache({ ...baseCacheEvent, GITHUB_REF: 'refs/heads/other' }, path),
    /only accepts/u,
  )
  assert.throws(
    () =>
      selectGradleTaskCache(
        { ...baseCacheEvent, GITHUB_EVENT_NAME: 'pull_request', PR_BASE_REVISION: '' },
        path,
      ),
    /exact base/u,
  )
  assert.throws(
    () => selectGradleTaskCache(baseCacheEvent, '/tmp/cache\ninvalid'),
    /single absolute/u,
  )
})

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
    const stateSelected = spawnSync(
      process.execPath,
      [
        '--input-type=module',
        '-e',
        `import { selectedBuildCacheRoot } from ${JSON.stringify(pathToFileURL(stateCli).href)}; process.stdout.write(selectedBuildCacheRoot())`,
      ],
      { env: ciEnv, encoding: 'utf8' },
    )
    assert.equal(stateSelected.status, 0, stateSelected.stderr)
    assert.equal(
      stateSelected.stdout,
      ciCacheDir,
      'exact-state codec must use the build cache path',
    )

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
