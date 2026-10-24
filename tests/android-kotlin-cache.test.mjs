import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { linkSync, unlinkSync, writeFileSync } from 'node:fs'
import { lstat, mkdir, readFile, readdir, realpath, rm, symlink, writeFile } from 'node:fs/promises'
import { dirname, join, relative } from 'node:path'
import test from 'node:test'
import {
  packKotlinCache,
  mergeKotlinCache,
} from '../swift-clients/apps/android/tooling/android-kotlin-cache.mjs'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'
import { cacheLimits } from '../swift-clients/apps/android/tooling/android-kotlin-cache-files.mjs'
import {
  cacheSaveLimits,
  inspectGradleBuildCache,
} from '../swift-clients/apps/android/tooling/check-gradle-build-cache.mjs'

const a = 'a'.repeat(32),
  b = 'b'.repeat(32),
  c = 'c'.repeat(32)
async function fixture(t) {
  const f = await androidStateFixture(t)
  const root = await realpath(f.root)
  for (const key of ['packageRoot', 'checkoutRoot', 'cacheRoot', 'artifactRoot'])
    f[key] = join(root, relative(f.root, f[key]))
  f.root = root
  f.configurationSha256 = 'd'.repeat(64)
  f.dependencySha256 = 'e'.repeat(64)
  await rm(join(f.cacheRoot, 'cache-entry'))
  await writeFile(join(f.cacheRoot, a), 'compiled library A')
  await writeFile(join(f.cacheRoot, b), 'compiled library B')
  await writeFile(join(f.cacheRoot, 'gc.properties'), '')
  await writeFile(join(f.cacheRoot, 'gradle-build-cache.lock'), 'owned lock metadata')
  return f
}
async function manifest(f, edit) {
  const path = join(f.artifactRoot, 'manifest.json')
  const value = JSON.parse(await readFile(path, 'utf8'))
  edit(value)
  await writeFile(path, JSON.stringify(value))
}
async function emptyCache(f) {
  await rm(f.cacheRoot, { recursive: true })
  await mkdir(f.cacheRoot)
}

test('flat handoff preserves exact outputs and existing entries without touching native or project state', async t => {
  const f = await fixture(t)
  assert.deepEqual(packKotlinCache(f), { role: 'kotlin-libraries', entries: 2, bytes: 36 })
  assert.deepEqual((await readdir(f.artifactRoot)).sort(), [a, b, 'manifest.json'])
  const header = JSON.parse(await readFile(join(f.artifactRoot, 'manifest.json')))
  assert.deepEqual(header.identity, f.identity)
  assert.equal(header.configurationSha256, f.configurationSha256)
  assert.equal(header.dependencySha256, f.dependencySha256)
  assert.deepEqual(header.requestedTasks, [':skipstone:SkipUI:compileDebugKotlin'])
  const before = await lstat(join(f.cacheRoot, a))
  await rm(join(f.cacheRoot, b))
  assert.deepEqual(mergeKotlinCache(f), { role: 'kotlin-libraries', added: 1, retained: 1 })
  assert.equal((await lstat(join(f.cacheRoot, a))).ino, before.ino)
  assert.equal(await readFile(join(f.cacheRoot, b), 'utf8'), 'compiled library B')
  assert.equal(
    await readFile(join(f.cacheRoot, 'gradle-build-cache.lock'), 'utf8'),
    'owned lock metadata',
  )
  assert.equal(
    await readFile(join(f.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
  assert.equal(
    await readFile(join(f.packageRoot, 'Android/.gradle/task-state'), 'utf8'),
    'task-state',
  )
})

test('all identity and role mismatches fail before cache publication', async t => {
  for (const field of [
    'revision',
    'run',
    'attempt',
    'gradle',
    'role',
    'configurationSha256',
    'dependencySha256',
  ]) {
    await t.test(field, async sub => {
      const f = await fixture(sub)
      packKotlinCache(f)
      await emptyCache(f)
      await manifest(f, m => {
        if (field in m.identity) m.identity[field] = 'different'
        else m[field] = 'different'
      })
      assert.throws(() => mergeKotlinCache(f), /identity mismatch/u)
      assert.deepEqual(await readdir(f.cacheRoot), [])
    })
  }
})

test('corrupt, missing, extra and unsafe artifact members all fail before publication', async t => {
  for (const kind of [
    'changed',
    'missing',
    'extra',
    'traversal',
    'duplicate',
    'symlink',
    'directory',
    'manifest-link',
    'null-manifest',
    'null-entry',
    'bad-size',
    'bad-hash',
  ]) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      packKotlinCache(f)
      await emptyCache(f)
      if (kind === 'null-manifest') await writeFile(join(f.artifactRoot, 'manifest.json'), 'null')
      if (kind === 'null-entry')
        await manifest(f, m => {
          m.entries[0] = null
        })
      if (kind === 'bad-size')
        await manifest(f, m => {
          m.entries[0].size = 'large'
        })
      if (kind === 'bad-hash')
        await manifest(f, m => {
          m.entries[0].sha256 = null
        })
      if (kind === 'changed') await writeFile(join(f.artifactRoot, b), 'corrupt')
      if (kind === 'missing') await rm(join(f.artifactRoot, b))
      if (kind === 'extra') await writeFile(join(f.artifactRoot, c), 'extra')
      if (kind === 'traversal')
        await manifest(f, m => {
          m.entries[0].name = '../escape'
        })
      if (kind === 'duplicate')
        await manifest(f, m => {
          m.entries.push(m.entries[0])
        })
      if (kind === 'symlink') {
        await rm(join(f.artifactRoot, b))
        await symlink(join(f.artifactRoot, a), join(f.artifactRoot, b))
      }
      if (kind === 'directory') {
        await rm(join(f.artifactRoot, b))
        await mkdir(join(f.artifactRoot, b))
      }
      if (kind === 'manifest-link') {
        await rm(join(f.artifactRoot, 'manifest.json'))
        await symlink(join(f.artifactRoot, a), join(f.artifactRoot, 'manifest.json'))
      }
      assert.throws(() => mergeKotlinCache(f))
      assert.deepEqual(await readdir(f.cacheRoot), [])
    })
  }
})

test('known metadata is omitted only when regular; unknown and transient source members fail closed', async t => {
  for (const kind of ['temp', 'directory', 'symlink', 'metadata-link']) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      if (kind === 'temp') await writeFile(join(f.cacheRoot, a + '-tmp'), 'partial')
      if (kind === 'directory') await mkdir(join(f.cacheRoot, c))
      if (kind === 'symlink') await symlink(join(f.cacheRoot, a), join(f.cacheRoot, c))
      if (kind === 'metadata-link') {
        await rm(join(f.cacheRoot, 'gc.properties'))
        await symlink(join(f.cacheRoot, a), join(f.cacheRoot, 'gc.properties'))
      }
      assert.throws(() => packKotlinCache(f))
      await assert.rejects(lstat(f.artifactRoot), { code: 'ENOENT' })
    })
  }
})

test('conflicting or symlinked destination entries fail full preflight before an earlier addition', async t => {
  for (const symbolic of [false, true]) {
    await t.test(String(symbolic), async sub => {
      const f = await fixture(sub)
      packKotlinCache(f)
      await emptyCache(f)
      if (symbolic) await symlink(join(f.artifactRoot, b), join(f.cacheRoot, b))
      else await writeFile(join(f.cacheRoot, b), 'foreign bytes')
      assert.throws(() => mergeKotlinCache(f))
      assert.deepEqual(await readdir(f.cacheRoot), [b])
      if (!symbolic) assert.equal(await readFile(join(f.cacheRoot, b), 'utf8'), 'foreign bytes')
    })
  }
})

test('publication failure rolls back only own additions and removes private staging', async t => {
  const f = await fixture(t)
  packKotlinCache(f)
  await emptyCache(f)
  await writeFile(join(f.cacheRoot, c), 'preexisting foreign')
  let count = 0
  assert.throws(
    () =>
      mergeKotlinCache({
        ...f,
        linkEntry(source, destination) {
          if (++count === 2) throw Error('injected publication failure')
          linkSync(source, destination)
        },
      }),
    /injected publication failure/u,
  )
  assert.equal(count, 2)
  assert.deepEqual(await readdir(f.cacheRoot), [c])
  assert.equal(await readFile(join(f.cacheRoot, c), 'utf8'), 'preexisting foreign')
  assert.ok(!(await readdir(dirname(f.cacheRoot))).some(name => name.startsWith('.kotlin-cache-')))
})

test('a racing destination is never overwritten, and rollback preserves a replaced foreign entry', async t => {
  const f = await fixture(t)
  packKotlinCache(f)
  await emptyCache(f)
  let count = 0
  assert.throws(
    () =>
      mergeKotlinCache({
        ...f,
        linkEntry(source, destination) {
          if (++count === 2) {
            unlinkSync(join(f.cacheRoot, a))
            writeFileSync(join(f.cacheRoot, a), 'foreign replacement')
            writeFileSync(destination, 'foreign race winner')
          }
          linkSync(source, destination)
        },
      }),
    { code: 'EEXIST' },
  )
  assert.equal(await readFile(join(f.cacheRoot, a), 'utf8'), 'foreign replacement')
  assert.equal(await readFile(join(f.cacheRoot, b), 'utf8'), 'foreign race winner')
})

test('entry, byte, manifest and time bounds reject without publishing', async t => {
  for (const limits of [{ entries: 1 }, { bytes: 1 }, { manifestBytes: 1 }, { ms: -1 }]) {
    await t.test(JSON.stringify(limits), async sub => {
      const f = await fixture(sub)
      assert.throws(() => packKotlinCache({ ...f, limits }), /limit/u)
      await assert.rejects(lstat(f.artifactRoot), { code: 'ENOENT' })
      packKotlinCache(f)
      await emptyCache(f)
      assert.throws(() => mergeKotlinCache({ ...f, limits }))
      assert.deepEqual(await readdir(f.cacheRoot), [])
    })
  }
})

test('empty task payloads are rejected by both producer and consumer', async t => {
  const f = await fixture(t)
  packKotlinCache(f)
  await emptyCache(f)
  assert.throws(
    () => packKotlinCache({ ...f, artifactRoot: join(f.root, 'empty-artifact') }),
    /no task entries/u,
  )
  await manifest(f, m => {
    m.entries = []
  })
  assert.throws(() => mergeKotlinCache(f), /manifest entries/u)
  assert.deepEqual(await readdir(f.cacheRoot), [])
})

test('special files and symlinked cache ancestors are rejected without opening or following them', async t => {
  const f = await fixture(t)
  const linked = join(f.root, 'linked-cache')
  await symlink(f.cacheRoot, linked)
  assert.throws(() => packKotlinCache({ ...f, cacheRoot: linked }), /ancestor/u)
  const fifo = spawnSync(
    'python3',
    ['-I', '-c', 'import os,sys; os.mkfifo(sys.argv[1])', join(f.cacheRoot, c)],
    { encoding: 'utf8' },
  )
  assert.equal(fifo.status, 0, fifo.stderr)
  assert.throws(() => packKotlinCache(f), /regular files/u)
  await assert.rejects(lstat(f.artifactRoot), { code: 'ENOENT' })
})

test('packing refuses to replace an existing artifact directory or its verified bytes', async t => {
  const f = await fixture(t)
  packKotlinCache(f)
  const before = await readFile(join(f.artifactRoot, 'manifest.json'), 'utf8')
  assert.throws(() => packKotlinCache(f), { code: 'EEXIST' })
  assert.equal(await readFile(join(f.artifactRoot, 'manifest.json'), 'utf8'), before)
  assert.equal(await readFile(join(f.artifactRoot, a), 'utf8'), 'compiled library A')
})

test('requested task provenance rejects missing, additional and different task selections', async t => {
  for (const requestedTasks of [
    [],
    [':skipstone:SkipLib:compileDebugKotlin'],
    [':skipstone:SkipUI:compileDebugKotlin', ':app:assembleDebug'],
  ]) {
    await t.test(JSON.stringify(requestedTasks), async sub => {
      const f = await fixture(sub)
      packKotlinCache(f)
      await emptyCache(f)
      await manifest(f, m => {
        m.requestedTasks = requestedTasks
      })
      assert.throws(() => mergeKotlinCache(f), /identity mismatch/u)
      assert.deepEqual(await readdir(f.cacheRoot), [])
    })
  }
})

test('handoff budgets cover the existing persistent cache contract and its manifest', () => {
  assert.equal(cacheLimits.entries, cacheSaveLimits.entries)
  assert.equal(cacheLimits.entries, 100_000)
  assert.equal(cacheLimits.bytes, cacheSaveLimits.bytes)
  assert.equal(cacheLimits.bytes, 512 * 1024 ** 2)
  assert.equal(cacheLimits.ms, cacheSaveLimits.scanMs)
  assert.equal(cacheLimits.ms, 30_000)
  const entries = Array.from({ length: cacheLimits.entries }, (_, index) => ({
    name: index.toString(16).padStart(32, '0'),
    size: cacheLimits.bytes,
    sha256: 'f'.repeat(64),
  }))
  // Use the largest admitted per-record size as a conservative encoding bound;
  // the real manifest also enforces the combined task-byte budget.
  const encoded = Buffer.byteLength(JSON.stringify({ entries })) + 1024
  assert.ok(encoded > 1024 ** 2, 'the old 1 MiB metadata budget cannot cover saved inventories')
  assert.ok(encoded <= cacheLimits.manifestBytes)
})

test('a save-eligible cache above 2000 tasks packs and merges without dropping entries', async t => {
  const f = await fixture(t)
  for (let index = 0; index < 1999; index++) {
    writeFileSync(join(f.cacheRoot, index.toString(16).padStart(32, '0')), 'x')
  }
  assert.equal(inspectGradleBuildCache(f.cacheRoot).eligible, true)
  assert.equal(packKotlinCache(f).entries, 2001)
  await emptyCache(f)
  assert.deepEqual(mergeKotlinCache(f), { role: 'kotlin-libraries', added: 2001, retained: 0 })
  assert.equal((await readdir(f.cacheRoot)).length, 2001)
  assert.equal(await readFile(join(f.cacheRoot, a), 'utf8'), 'compiled library A')
})

test('merged task count accepts its boundary and rejects overflow before publication', async t => {
  for (const overflow of [false, true]) {
    await t.test(String(overflow), async sub => {
      const f = await fixture(sub)
      packKotlinCache(f)
      await emptyCache(f)
      await writeFile(join(f.cacheRoot, c), 'existing')
      await writeFile(join(f.cacheRoot, 'gc.properties'), 'metadata')
      if (overflow) await writeFile(join(f.cacheRoot, 'd'.repeat(32)), 'another existing')
      const before = (await readdir(f.cacheRoot)).sort()
      const options = { ...f, limits: { entries: 3 } }
      if (overflow) {
        assert.throws(() => mergeKotlinCache(options), /Merged Kotlin cache exceeds bounds/u)
        assert.deepEqual((await readdir(f.cacheRoot)).sort(), before)
        assert.equal(await readFile(join(f.cacheRoot, c), 'utf8'), 'existing')
      } else {
        assert.deepEqual(mergeKotlinCache(options), {
          role: 'kotlin-libraries',
          added: 2,
          retained: 0,
        })
      }
    })
  }
})

test('manifest has its own byte budget while root metadata remains in merged byte preflight', async t => {
  const f = await fixture(t)
  packKotlinCache(f)
  await emptyCache(f)
  await writeFile(join(f.cacheRoot, 'gc.properties'), 'x')
  const payloadBytes = 36
  // The separately validated manifest is larger than this tiny task-byte budget.
  assert.ok((await lstat(join(f.artifactRoot, 'manifest.json'))).size > payloadBytes)
  assert.throws(
    () => mergeKotlinCache({ ...f, limits: { bytes: payloadBytes } }),
    /Merged Kotlin cache exceeds bounds/u,
  )
  assert.deepEqual(await readdir(f.cacheRoot), ['gc.properties'])
  assert.equal(await readFile(join(f.cacheRoot, 'gc.properties'), 'utf8'), 'x')
  assert.deepEqual(mergeKotlinCache({ ...f, limits: { bytes: payloadBytes + 1 } }), {
    role: 'kotlin-libraries',
    added: 2,
    retained: 0,
  })
})
