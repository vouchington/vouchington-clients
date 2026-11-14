import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { lstat, mkdir, readFile, readdir, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import { dirname, join, relative } from 'node:path'
import test from 'node:test'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'
import {
  mergeKotlinCache,
  packKotlinCache,
  restoreKotlinCache,
} from '../swift-clients/apps/android/tooling/android-kotlin-cache.mjs'

const a = 'a'.repeat(32),
  b = 'b'.repeat(32)
async function fixture(t) {
  const f = await androidStateFixture(t)
  const root = await realpath(f.root)
  for (const key of ['packageRoot', 'checkoutRoot', 'cacheRoot', 'artifactRoot'])
    f[key] = join(root, relative(f.root, f[key]))
  f.root = root
  f.configurationSha256 = 'd'.repeat(64)
  f.dependencySha256 = 'e'.repeat(64)
  await rm(join(f.cacheRoot, 'cache-entry'))
  await writeFile(join(f.cacheRoot, a), 'exact Kotlin output')
  packKotlinCache(f)
  await writeFile(join(f.cacheRoot, a), 'older conflicting output')
  await writeFile(join(f.cacheRoot, b), 'older extra output')
  return f
}

async function unchanged(f) {
  assert.equal(await readFile(join(f.cacheRoot, a), 'utf8'), 'older conflicting output')
  assert.equal(await readFile(join(f.cacheRoot, b), 'utf8'), 'older extra output')
}

test('restore publishes authoritative exact-run bytes while merge still rejects the conflict', async t => {
  const f = await fixture(t)
  await writeFile(join(f.cacheRoot, 'gradle-build-cache.lock'), 'existing Gradle lock state')
  const lock = await lstat(join(f.cacheRoot, 'gradle-build-cache.lock'))
  assert.throws(() => mergeKotlinCache(f), /Conflicting existing Kotlin cache entry/u)
  assert.deepEqual(restoreKotlinCache(f), { role: 'kotlin-libraries', restored: 1, bytes: 19 })
  assert.deepEqual((await readdir(f.cacheRoot)).sort(), [a, 'gradle-build-cache.lock'])
  assert.equal(await readFile(join(f.cacheRoot, a), 'utf8'), 'exact Kotlin output')
  assert.equal((await lstat(join(f.cacheRoot, 'gradle-build-cache.lock'))).ino, lock.ino)
  assert.equal(
    await readFile(join(f.cacheRoot, 'gradle-build-cache.lock'), 'utf8'),
    'existing Gradle lock state',
  )
  assert.equal(
    await readFile(join(f.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
  assert.equal(
    await readFile(join(f.packageRoot, 'Android/.gradle/task-state'), 'utf8'),
    'task-state',
  )
  assert.equal(restoreKotlinCache(f).restored, 1, 'the same exact artifact restores idempotently')
  assert.ok(
    !(await readdir(dirname(f.cacheRoot))).some(name => name.startsWith('.kotlin-cache-restore-')),
  )
})

test('identity, payload and bounded inventory failures leave the existing cache untouched', async t => {
  for (const kind of [
    'identity',
    'gradle',
    'dependency',
    'configuration',
    'corrupt',
    'extra',
    'symlink',
    'bounds',
  ]) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      const options = { ...f }
      if (kind === 'identity') options.identity = { ...f.identity, attempt: '999' }
      if (kind === 'gradle') options.identity = { ...f.identity, gradle: '99.0.0' }
      if (kind === 'dependency') options.dependencySha256 = 'f'.repeat(64)
      if (kind === 'configuration') options.configurationSha256 = 'f'.repeat(64)
      if (kind === 'corrupt') await writeFile(join(f.artifactRoot, a), 'corrupt')
      if (kind === 'extra') await writeFile(join(f.artifactRoot, b), 'extra')
      if (kind === 'symlink') {
        await rm(join(f.artifactRoot, a))
        await symlink(join(f.cacheRoot, a), join(f.artifactRoot, a))
      }
      if (kind === 'bounds') options.limits = { bytes: 1 }
      assert.throws(() => restoreKotlinCache(options))
      await unchanged(f)
      assert.deepEqual((await readdir(f.cacheRoot)).sort(), [a, b])
    })
  }
})

test('unsafe destination ancestors and members are rejected without replacement', async t => {
  for (const kind of ['ancestor', 'member', 'metadata']) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      let cacheRoot = f.cacheRoot
      if (kind === 'ancestor') {
        const linked = join(f.root, 'linked-parent')
        await symlink(dirname(cacheRoot), linked)
        cacheRoot = join(linked, 'gradle-build-cache')
      } else
        await symlink(
          join(f.artifactRoot, a),
          join(cacheRoot, kind === 'metadata' ? 'gradle-build-cache.lock' : 'c'.repeat(32)),
        )
      assert.throws(() => restoreKotlinCache({ ...f, cacheRoot }))
      await unchanged(f)
    })
  }
})

test('an active Gradle record lock blocks restore and preserves all existing bytes', async t => {
  const f = await fixture(t)
  const lockPath = join(f.cacheRoot, 'gradle-build-cache.lock')
  await writeFile(lockPath, 'Gradle lock state')
  const holder = spawn(
    'python3',
    [
      '-I',
      '-c',
      'import fcntl,sys; f=open(sys.argv[1],"r+"); fcntl.lockf(f,fcntl.LOCK_EX); print("locked",flush=True); sys.stdin.read()',
      lockPath,
    ],
    { stdio: ['pipe', 'pipe', 'pipe'] },
  )
  t.after(() => holder.kill())
  await new Promise((resolve, reject) => {
    holder.once('error', reject)
    holder.stdout.once('data', resolve)
    holder.once('exit', code => reject(Error(`lock holder exited ${code}`)))
  })
  assert.throws(() => restoreKotlinCache(f), /publication failed/u)
  await unchanged(f)
  assert.equal(await readFile(lockPath, 'utf8'), 'Gradle lock state')
  assert.ok(!(await readdir(dirname(f.cacheRoot))).some(name => name.endsWith('.restore-lock')))
})

test('a concurrent restore owner is never bypassed or removed', async t => {
  const f = await fixture(t)
  const guard = f.cacheRoot + '.restore-lock'
  await mkdir(guard)
  await writeFile(join(guard, 'owner'), 'another restore')
  assert.throws(() => restoreKotlinCache(f), /publication failed/u)
  await unchanged(f)
  assert.equal(await readFile(join(guard, 'owner'), 'utf8'), 'another restore')
})

test('restored byte budget includes the preserved Gradle lock metadata', async t => {
  const f = await fixture(t)
  await rm(join(f.cacheRoot, a))
  await rm(join(f.cacheRoot, b))
  await writeFile(join(f.cacheRoot, 'gradle-build-cache.lock'), 'x')
  assert.throws(
    () => restoreKotlinCache({ ...f, limits: { bytes: 19 } }),
    /Restored Kotlin cache exceeds bounds/u,
  )
  assert.deepEqual(await readdir(f.cacheRoot), ['gradle-build-cache.lock'])
  assert.equal(restoreKotlinCache({ ...f, limits: { bytes: 20 } }).restored, 1)
})
