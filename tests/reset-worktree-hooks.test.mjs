import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { chmod, cp, mkdir, mkdtemp, readFile, rm, stat, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const sourceRoot = fileURLToPath(new URL('..', import.meta.url))
const { HUSKY: _ignored, ...normalEnvironment } = process.env

async function git(directory, ...arguments_) {
  return execFileAsync('git', arguments_, { cwd: directory })
}

async function fixture(t) {
  const directory = await mkdtemp(join(tmpdir(), 'voucha-reset-hooks-'))
  t.after(() => rm(directory, { recursive: true, force: true }))
  const primary = join(directory, 'primary')
  const linked = join(directory, 'linked')
  const remote = join(directory, 'remote.git')
  const bin = join(directory, 'bin')
  await mkdir(primary)
  await mkdir(bin)
  await git(directory, 'init', '-q', '-b', 'main', primary)
  for (const path of [
    'dev/reset-worktree',
    'dev/lib/clean-build-output.sh',
    'scripts/prepare-hooks.mjs',
    '.husky/pre-push',
  ]) {
    await mkdir(join(primary, dirname(path)), { recursive: true })
    await cp(join(sourceRoot, path), join(primary, path))
  }
  await mkdir(join(primary, 'swift-clients/apps/android'), { recursive: true })
  await mkdir(join(primary, 'dotnet-clients'))
  await writeFile(join(primary, 'swift-clients/apps/android/.keep'), '')
  await writeFile(join(primary, 'dotnet-clients/.keep'), '')
  await writeFile(join(primary, '.gitignore'), 'node_modules/\n')
  await git(primary, 'add', '.')
  await git(
    primary,
    '-c',
    'user.name=Fixture',
    '-c',
    'user.email=fixture@example.test',
    'commit',
    '-qm',
    'fixture',
  )
  await git(directory, 'init', '-q', '--bare', remote)
  await git(primary, 'remote', 'add', 'origin', remote)
  await git(primary, 'push', '-q', '-u', 'origin', 'main')
  await git(primary, 'worktree', 'add', '-q', '--detach', linked, 'HEAD')

  const pnpm = join(bin, 'pnpm')
  await writeFile(
    pnpm,
    `#!/bin/sh
set -eu
printf '%s\\n' "$3" >> "$VOUCHA_FIXTURE_CALLS"
if [ "$3" = install ]; then
  ln -s "$VOUCHA_FIXTURE_NODE_MODULES" "$2/node_modules"
elif [ "$3" = run ] && [ "$4" = prepare ]; then
  [ "${'${VOUCHA_FIXTURE_PREPARE_FAIL:-}'}" != 1 ] || exit 7
  cd "$2"
  exec "$VOUCHA_FIXTURE_NODE" scripts/prepare-hooks.mjs
else
  exit 9
fi
`,
  )
  await chmod(pnpm, 0o755)
  return {
    linked,
    calls: join(directory, 'calls'),
    env: {
      ...normalEnvironment,
      PATH: `${bin}:${process.env.PATH}`,
      VOUCHA_FIXTURE_CALLS: join(directory, 'calls'),
      VOUCHA_FIXTURE_NODE_MODULES: join(sourceRoot, 'node_modules'),
      VOUCHA_FIXTURE_NODE: process.execPath,
    },
  }
}

test('cached install without lifecycle still prepares a linked worktree hook', async t => {
  const { linked, calls, env } = await fixture(t)
  await execFileAsync('bash', ['dev/reset-worktree'], { cwd: linked, env })

  assert.deepEqual((await readFile(calls, 'utf8')).trim().split('\n'), ['install', 'run'])
  const { stdout } = await git(linked, 'config', '--get', 'core.hooksPath')
  assert.equal(stdout.trim(), '.husky/_')
  assert.ok((await stat(join(linked, '.husky/_/pre-push'))).mode & 0o111)
})

test('reset fails closed when preparation fails after a cached install', async t => {
  const { linked, calls, env } = await fixture(t)
  await assert.rejects(
    execFileAsync('bash', ['dev/reset-worktree'], {
      cwd: linked,
      env: { ...env, VOUCHA_FIXTURE_PREPARE_FAIL: '1' },
    }),
    error => {
      assert.equal(error.code, 7)
      assert.doesNotMatch(error.stdout, /Worktree reset to origin\/main/u)
      return true
    },
  )
  assert.deepEqual((await readFile(calls, 'utf8')).trim().split('\n'), ['install', 'run'])
  await assert.rejects(stat(join(linked, '.husky/_/pre-push')), { code: 'ENOENT' })
})
