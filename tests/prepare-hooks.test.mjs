import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { cp, mkdir, mkdtemp, readFile, rm, stat, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import test from 'node:test'

const execFileAsync = promisify(execFile)
const prepareScript = fileURLToPath(new URL('../scripts/prepare-hooks.mjs', import.meta.url))
const { HUSKY: _ignored, ...normalEnvironment } = process.env

async function fixture(t) {
  const directory = await mkdtemp(join(tmpdir(), 'voucha-hook-bootstrap-'))
  t.after(() => rm(directory, { recursive: true, force: true }))
  await execFileAsync('git', ['init', '-q', directory])
  await mkdir(join(directory, '.husky'))
  await writeFile(join(directory, '.husky/pre-push'), 'echo checked\n')
  return directory
}

async function prepare(directory, environment = {}) {
  return execFileAsync(process.execPath, [prepareScript], {
    cwd: directory,
    env: { ...normalEnvironment, ...environment },
  })
}

test('fresh worktree preparation installs a usable pre-push wrapper', async t => {
  const directory = await fixture(t)
  await prepare(directory)

  const { stdout } = await execFileAsync('git', ['config', '--get', 'core.hooksPath'], {
    cwd: directory,
  })
  assert.equal(stdout.trim(), '.husky/_')
  const wrapper = await stat(join(directory, '.husky/_/pre-push'))
  assert.ok(wrapper.isFile())
  if (process.platform !== 'win32') assert.ok(wrapper.mode & 0o111)
  assert.match(await readFile(join(directory, '.husky/_/h'), 'utf8'), /HUSKY/u)
})

test('a Git config lock failure rejects preparation even when Husky reports an error as text', async t => {
  const directory = await fixture(t)
  await mkdir(join(directory, '.git/config.lock'))

  await assert.rejects(prepare(directory), error => {
    assert.notEqual(error.code, 0)
    assert.match(error.stderr, /Husky hook setup failed.*(?:lock|config)/isu)
    return true
  })
  await assert.rejects(stat(join(directory, '.husky/_/pre-push')), { code: 'ENOENT' })
})

test('verification rejects a missing wrapper or mismatched effective hooksPath', async t => {
  const directory = await fixture(t)
  await prepare(directory)
  const { verifyHookInstall } = await import('../scripts/prepare-hooks.mjs')

  await rm(join(directory, '.husky/_/pre-push'))
  await assert.rejects(verifyHookInstall(directory), /pre-push.*missing/iu)

  await prepare(directory)
  await execFileAsync('git', ['config', 'core.hooksPath', '.git/hooks'], { cwd: directory })
  await assert.rejects(verifyHookInstall(directory), /core\.hooksPath.*\.husky\/_/u)
})

test('explicit HUSKY=0 bypasses hook installation without hiding a normal failure', async t => {
  const directory = await fixture(t)
  await mkdir(join(directory, '.git/config.lock'))
  const isolatedScript = join(directory, 'prepare-hooks.mjs')
  await cp(prepareScript, isolatedScript)

  await execFileAsync(process.execPath, [isolatedScript], {
    cwd: directory,
    env: { ...normalEnvironment, HUSKY: '0' },
  })
  await assert.rejects(stat(join(directory, '.husky/_/pre-push')), { code: 'ENOENT' })
  await assert.rejects(
    execFileAsync('git', ['config', '--get', 'core.hooksPath'], {
      cwd: directory,
    }),
  )
})
