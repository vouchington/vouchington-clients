import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdir, readFile, readdir, realpath, rm, writeFile } from 'node:fs/promises'
import { dirname, join, relative } from 'node:path'
import test from 'node:test'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'
import {
  packKotlinCache,
  restoreKotlinCache,
} from '../swift-clients/apps/android/tooling/android-kotlin-cache.mjs'

const key = 'a'.repeat(32)
async function fixture(t) {
  const f = await androidStateFixture(t)
  const root = await realpath(f.root)
  for (const name of ['packageRoot', 'checkoutRoot', 'cacheRoot', 'artifactRoot'])
    f[name] = join(root, relative(f.root, f[name]))
  f.root = root
  f.configurationSha256 = 'd'.repeat(64)
  f.dependencySha256 = 'e'.repeat(64)
  await rm(join(f.cacheRoot, 'cache-entry'))
  await writeFile(join(f.cacheRoot, key), 'incoming verified payload')
  packKotlinCache(f)
  await writeFile(join(f.cacheRoot, key), 'original existing payload')
  await writeFile(join(f.cacheRoot, 'gradle-build-cache.lock'), 'original lock')
  return f
}

// Interpose only the Python process boundary, as the CLI tests interpose uname.
// Audit hooks and ctypes wrappers schedule actual filesystem races at the syscall;
// the production API has no test-only callbacks or alternate publication path.
async function pythonBoundary(f, prefix, operation) {
  const realPython = spawnSync('python3', ['-I', '-c', 'import sys; print(sys.executable)'], {
    encoding: 'utf8',
  })
  assert.equal(realPython.status, 0, realPython.stderr)
  const bin = join(f.root, 'python-boundary')
  await mkdir(bin)
  await writeFile(
    join(bin, 'python3'),
    `#!${realPython.stdout.trim()}\nimport sys\nsource = sys.argv[sys.argv.index('-c') + 1]\nexec(${JSON.stringify(prefix)} + '\\n' + source)\n`,
    { mode: 0o700 },
  )
  const previousPath = process.env.PATH
  process.env.PATH = `${bin}:${previousPath}`
  try {
    operation()
  } finally {
    process.env.PATH = previousPath
  }
}

test('a lock pathname replaced after open cannot publish a different inode or symlink', async t => {
  for (const symbolic of [false, true]) {
    await t.test(String(symbolic), async sub => {
      const f = await fixture(sub)
      const lockPath = join(f.cacheRoot, 'gradle-build-cache.lock')
      const prefix = `
import os, sys
changed = False
def race(event, args):
    global changed
    if event == 'os.link' and not changed:
        changed = True
        os.unlink(args[0])
        ${symbolic ? "os.symlink('foreign-target', args[0])" : "open(args[0], 'w').write('foreign lock inode')"}
sys.addaudithook(race)
`
      await pythonBoundary(f, prefix, () =>
        assert.throws(() => restoreKotlinCache(f), /Gradle cache lock changed/u),
      )
      assert.equal(await readFile(join(f.cacheRoot, key), 'utf8'), 'original existing payload')
      if (!symbolic) assert.equal(await readFile(lockPath, 'utf8'), 'foreign lock inode')
      assert.ok(
        !(await readdir(dirname(f.cacheRoot))).some(name =>
          name.startsWith('.kotlin-cache-restore-'),
        ),
      )
    })
  }
})

function exchangeRace(root, secondReplacement) {
  return `
import ctypes, os
real_library = ctypes.CDLL
raced = False
class Exchange:
    def __init__(self, fn): self.fn = fn
    @property
    def argtypes(self): return self.fn.argtypes
    @argtypes.setter
    def argtypes(self, value): self.fn.argtypes = value
    def __call__(self, *args):
        global raced
        if raced: return self.fn(*args)
        raced = True
        root = ${JSON.stringify(root)}
        os.rename(root, root + '-original')
        os.mkdir(root)
        open(os.path.join(root, 'foreign-B'), 'w').write('foreign B bytes')
        result = self.fn(*args)
        if ${secondReplacement ? 'True' : 'False'}:
            os.rename(root, root + '-published')
            os.mkdir(root)
            open(os.path.join(root, 'foreign-C'), 'w').write('foreign C bytes')
        return result
class Library:
    def __init__(self, *args, **kwargs): self.lib = real_library(*args, **kwargs)
    def __getattr__(self, name):
        fn = getattr(self.lib, name)
        return Exchange(fn) if name in ['renamex_np', 'renameat2'] else fn
ctypes.CDLL = Library
`
}

test('a destination exchanged after the final guard is rolled back without deleting foreign bytes', async t => {
  const f = await fixture(t)
  await pythonBoundary(f, exchangeRace(f.cacheRoot, false), () =>
    assert.throws(() => restoreKotlinCache(f), /destination changed during publication/u),
  )
  assert.equal(await readFile(join(f.cacheRoot, 'foreign-B'), 'utf8'), 'foreign B bytes')
  assert.equal(
    await readFile(join(f.cacheRoot + '-original', key), 'utf8'),
    'original existing payload',
  )
  assert.ok(
    !(await readdir(dirname(f.cacheRoot))).some(name => name.startsWith('.kotlin-cache-restore-')),
  )
})

test('a second late replacement retains the unexpected exchanged directory for recovery', async t => {
  const f = await fixture(t)
  await pythonBoundary(f, exchangeRace(f.cacheRoot, true), () =>
    assert.throws(() => restoreKotlinCache(f), /destination changed during publication/u),
  )
  assert.equal(await readFile(join(f.cacheRoot, 'foreign-C'), 'utf8'), 'foreign C bytes')
  assert.equal(
    await readFile(join(f.cacheRoot + '-original', key), 'utf8'),
    'original existing payload',
  )
  assert.equal(
    await readFile(join(f.cacheRoot + '-published', key), 'utf8'),
    'incoming verified payload',
  )
  const retained = (await readdir(dirname(f.cacheRoot))).filter(name =>
    name.startsWith('.kotlin-cache-restore-'),
  )
  assert.equal(retained.length, 1)
  assert.equal(
    await readFile(join(dirname(f.cacheRoot), retained[0], 'foreign-B'), 'utf8'),
    'foreign B bytes',
  )
})
