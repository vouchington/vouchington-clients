import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { chmod, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const withBuildLock = join(repositoryRoot, 'swift-clients/tooling/with-build-lock.sh')

async function writeExecutable(path, contents) {
  await writeFile(path, contents)
  await chmod(path, 0o755)
}

async function lockFixture(t) {
  const binDirectory = await mkdtemp(join(tmpdir(), 'voucha-build-lock-'))
  const argumentsPath = join(binDirectory, 'npx-arguments.txt')
  const localEnvironment = { ...process.env }
  delete localEnvironment.GITHUB_ACTIONS
  await writeExecutable(
    join(binDirectory, 'npx'),
    '#!/usr/bin/env bash\nprintf \'%s\\n\' "$@" > "$NPX_ARGUMENTS_PATH"\n',
  )
  t.after(() => rm(binDirectory, { recursive: true, force: true }))
  return {
    argumentsPath,
    environment: {
      ...localEnvironment,
      PATH: `${binDirectory}:${process.env.PATH}`,
      NPX_ARGUMENTS_PATH: argumentsPath,
    },
  }
}

async function invokeLock(environment) {
  await execFileAsync('bash', [withBuildLock, 'echo', 'ok'], {
    cwd: repositoryRoot,
    env: environment,
  })
}

test('rejects leading-zero wait durations before numeric comparison', async () => {
  await assert.rejects(
    invokeLock({ ...process.env, VOUCHA_BUILD_LOCK_WAIT_SECONDS: '08' }),
    /VOUCHA_BUILD_LOCK_WAIT_SECONDS must be a positive integer no greater than 300/,
  )
})

test('passes the validated effective local and CI command timeouts to the host lock', async t => {
  const fixture = await lockFixture(t)
  await invokeLock(fixture.environment)
  assert.deepEqual((await readFile(fixture.argumentsPath, 'utf8')).trim().split('\n'), [
    '--yes',
    'pnpm@11.13.1',
    'exec',
    'vouchington',
    'with-host-lock',
    '--name',
    'expensive-build',
    '--timeout-seconds',
    '60',
    '--command-timeout-seconds',
    '0',
    '--on-acquire-timeout',
    'fail',
    '--',
    'echo',
    'ok',
  ])

  await invokeLock({ ...fixture.environment, GITHUB_ACTIONS: 'true' })
  assert.deepEqual(
    (await readFile(fixture.argumentsPath, 'utf8')).trim().split('\n').slice(9, 15),
    ['--command-timeout-seconds', '300', '--on-acquire-timeout', 'run-unlocked', '--', 'echo'],
  )
})

test('rejects a nonnumeric final CI command timeout', async () => {
  await assert.rejects(
    invokeLock({
      ...process.env,
      GITHUB_ACTIONS: 'true',
      VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS: 'five minutes',
    }),
    /VOUCHA_BUILD_LOCK_COMMAND_TIMEOUT_SECONDS must be a nonnegative integer/,
  )
})
