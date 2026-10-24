import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import {
  chmod,
  lstat,
  mkdir,
  readFile,
  readlink,
  rename,
  rm,
  stat,
  symlink,
  utimes,
  writeFile,
} from 'node:fs/promises'
import { join } from 'node:path'
import test from 'node:test'
import { gunzipSync, gzipSync } from 'node:zlib'
import {
  packState,
  restoreState,
  inventoryState,
} from '../swift-clients/apps/android/tooling/android-state-handoff.mjs'
import {
  testRunnerLimits,
  testRunnerDebugTarget,
} from '../swift-clients/apps/android/tooling/android-state-role.mjs'
import { assertDebugLink } from '../swift-clients/apps/android/tooling/android-state-links.mjs'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'

async function fixture(t) {
  const f = await androidStateFixture(t)
  f.role = 'test-runner'
  f.identity = { ...f.identity, role: f.role }
  f.products = join(f.packageRoot, '.build/out/Products/Debug-android-aarch64')
  await mkdir(f.products, { recursive: true })
  await writeFile(join(f.products, 'VouchaAndroidTests-test-runner'), 'verified runner')
  await chmod(join(f.products, 'VouchaAndroidTests-test-runner'), 0o755)
  await symlink('out/Products/Debug-android-aarch64', join(f.packageRoot, '.build/debug'))
  return f
}

async function rewrite(f, edit, headerIndex = 0) {
  const archive = join(f.artifactRoot, 'android-state.gz')
  const bytes = gunzipSync(await readFile(archive))
  let offset = 5
  for (let index = 0; ; index++) {
    assert.ok(index < 100, 'fixture member must exist')
    const length = bytes.readUInt32BE(offset)
    const header = JSON.parse(bytes.subarray(offset + 4, offset + 4 + length).toString())
    if (typeof headerIndex === 'string' ? header.path === headerIndex : index === headerIndex) break
    offset += 4 + length + (['file', 'source-metadata'].includes(header.type) ? header.size : 0)
  }
  const length = bytes.readUInt32BE(offset)
  const header = JSON.parse(bytes.subarray(offset + 4, offset + 4 + length).toString())
  edit(header)
  const changed = Buffer.from(JSON.stringify(header)),
    size = Buffer.alloc(4)
  size.writeUInt32BE(changed.length)
  const packed = gzipSync(
    Buffer.concat([bytes.subarray(0, offset), size, changed, bytes.subarray(offset + 4 + length)]),
  )
  await writeFile(archive, packed)
  const path = join(f.artifactRoot, 'android-state.json')
  const manifest = JSON.parse(await readFile(path, 'utf8'))
  manifest.sha256 = createHash('sha256').update(packed).digest('hex')
  manifest.compressedBytes = packed.length
  await writeFile(path, JSON.stringify(manifest))
}

test('test-runner role restores only .build and reconstructs the existing checked compatibility alias', async t => {
  const f = await fixture(t)
  const members = inventoryState(
    f.packageRoot,
    f.cacheRoot,
    f.checkoutRoot,
    testRunnerLimits,
    f.role,
  )
  assert.ok(
    members.entries.every(entry => entry.path === '.build' || entry.path.startsWith('.build/')),
  )
  const packed = await packState(f)
  assert.equal(packed.role, 'test-runner')
  assert.deepEqual(packed.identity, f.identity)
  const sourceTime = (await stat(join(f.packageRoot, '.build'), { bigint: true })).mtimeNs
  await rm(join(f.packageRoot, '.build'), { recursive: true })
  await restoreState(f)
  assert.equal(
    await readlink(join(f.packageRoot, '.build/debug')),
    'out/Products/Debug-android-aarch64',
  )
  assert.equal((await stat(join(f.packageRoot, '.build'), { bigint: true })).mtimeNs, sourceTime)
  assert.equal(
    await readFile(join(f.products, 'VouchaAndroidTests-test-runner'), 'utf8'),
    'verified runner',
  )
  assert.equal(
    await readFile(join(f.packageRoot, 'Android/.gradle/task-state'), 'utf8'),
    'task-state',
  )
  assert.equal(await readFile(join(f.cacheRoot, 'cache-entry'), 'utf8'), 'cache')
  const alias = join(f.products, 'voucha-androidPackageTests.xctest')
  await assert.rejects(lstat(alias), { code: 'ENOENT' })
  const linked = spawnSync(
    'bash',
    ['swift-clients/apps/android/tooling/link-swift64-android-test-runner.sh', f.packageRoot],
    { encoding: 'utf8' },
  )
  assert.equal(linked.status, 0, linked.stderr)
  assert.equal(await readlink(alias), 'VouchaAndroidTests-test-runner')
})

test('native and runner roles reject substitution in both directions before destination changes', async t => {
  for (const runner of [false, true]) {
    await t.test(String(runner), async sub => {
      const f = await fixture(sub)
      const native = { ...f, role: 'native', identity: { ...f.identity } }
      delete native.identity.role
      if (!runner) await rm(join(f.packageRoot, '.build/debug'))
      const packed = runner ? f : native
      const expected = runner ? native : f
      await packState(packed)
      await assert.rejects(restoreState(expected), /identity or limits mismatch/u)
      assert.equal(
        await readFile(join(f.products, 'VouchaAndroidTests-test-runner'), 'utf8'),
        'verified runner',
      )
      await assert.rejects(packState({ ...f, role: 'native' }), /role identity mismatch/u)
      await assert.rejects(packState({ ...native, role: 'test-runner' }), /role identity mismatch/u)
    })
  }
})

test('wrong head, run, attempt, Gradle and manifest role leave source timestamps and state untouched', async t => {
  const changes = [
    { revision: 'f'.repeat(40) },
    { run: '999' },
    { attempt: '999' },
    { gradle: '9.9.9' },
  ]
  for (const change of changes) {
    await t.test(JSON.stringify(change), async sub => {
      const f = await fixture(sub)
      await packState(f)
      const source = join(f.checkoutRoot, 'swift-clients/Package.swift')
      await utimes(source, 100, 100)
      await assert.rejects(
        restoreState({ ...f, identity: { ...f.identity, ...change } }),
        /identity or limits mismatch/u,
      )
      assert.equal((await stat(source)).mtimeMs, 100000)
    })
  }
  for (const role of ['native', 'unknown', null]) {
    await t.test(String(role), async sub => {
      const f = await fixture(sub)
      await packState(f)
      const path = join(f.artifactRoot, 'android-state.json')
      const manifest = JSON.parse(await readFile(path, 'utf8'))
      manifest.role = role
      await writeFile(path, JSON.stringify(manifest))
      await assert.rejects(restoreState(f), /identity or limits mismatch/u)
    })
  }
})

test('runner role rejects unknown or enlarged budgets before pack or restore I/O', async t => {
  for (const limits of [
    ...Object.keys(testRunnerLimits).map(key => ({
      ...testRunnerLimits,
      [key]: testRunnerLimits[key] + 1,
    })),
    { ...testRunnerLimits, unknown: 1 },
  ]) {
    await t.test(JSON.stringify(limits), async sub => {
      const f = await fixture(sub)
      await assert.rejects(packState({ ...f, limits }), /role bounds/u)
      await assert.rejects(restoreState({ ...f, limits }), /role bounds/u)
      await assert.rejects(lstat(f.artifactRoot), { code: 'ENOENT' })
    })
  }
})

test('runner role rejects existing destinations before applying tracked timestamps', async t => {
  for (const kind of ['empty', 'foreign', 'symlink']) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      await packState(f)
      await rm(join(f.packageRoot, '.build'), { recursive: true })
      if (kind === 'symlink') await symlink(f.cacheRoot, join(f.packageRoot, '.build'))
      else await mkdir(join(f.packageRoot, '.build'))
      if (kind === 'foreign') await writeFile(join(f.packageRoot, '.build/sentinel'), 'foreign')
      const source = join(f.checkoutRoot, 'swift-clients/Package.swift')
      await utimes(source, 100, 100)
      await assert.rejects(restoreState(f), /destination already exists/u)
      assert.equal((await stat(source)).mtimeMs, 100000)
      if (kind === 'foreign')
        assert.equal(await readFile(join(f.packageRoot, '.build/sentinel'), 'utf8'), 'foreign')
      if (kind === 'symlink')
        assert.equal(await readlink(join(f.packageRoot, '.build')), f.cacheRoot)
    })
  }
})

test('runner role keeps header identity, archive root and unknown .out links fail-closed', async t => {
  for (const kind of ['header-role', 'foreign-root', 'out-link']) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      if (kind === 'out-link') {
        await symlink(
          'VouchaAndroidTests-test-runner',
          join(f.products, 'voucha-androidPackageTests.xctest'),
        )
        await assert.rejects(packState(f), /Unsafe Android state symlink/u)
        return
      }
      await packState(f)
      await rm(join(f.packageRoot, '.build'), { recursive: true })
      if (kind === 'header-role')
        await rewrite(f, header => {
          delete header.role
        })
      else
        await rewrite(
          f,
          header => {
            header.path = 'Android'
          },
          1,
        )
      await assert.rejects(restoreState(f), /header identity mismatch|only admits .build/u)
      await assert.rejects(lstat(join(f.packageRoot, '.build')), { code: 'ENOENT' })
    })
  }
})

test('runner manifest cannot enlarge role budgets or hide a different source-metadata identity', async t => {
  for (const field of ['entries', 'unpackedBytes', 'compressedBytes', 'source-role']) {
    await t.test(field, async sub => {
      const f = await fixture(sub)
      await packState(f)
      await rm(join(f.packageRoot, '.build'), { recursive: true })
      const manifestPath = join(f.artifactRoot, 'android-state.json')
      const manifest = JSON.parse(await readFile(manifestPath, 'utf8'))
      if (field === 'source-role') {
        const archivePath = join(f.artifactRoot, 'android-state.gz')
        const bytes = gunzipSync(await readFile(archivePath))
        const token = Buffer.from('"role":"test-runner"')
        const offset = bytes.lastIndexOf(token)
        assert.ok(offset > 5 + bytes.readUInt32BE(5), 'edit only the source-metadata payload')
        Buffer.from('"role":"test-forged"').copy(bytes, offset)
        const packed = gzipSync(bytes)
        await writeFile(archivePath, packed)
        manifest.sha256 = createHash('sha256').update(packed).digest('hex')
        manifest.compressedBytes = packed.length
      } else manifest[field] = testRunnerLimits[field] + 1
      await writeFile(manifestPath, JSON.stringify(manifest))
      const source = join(f.checkoutRoot, 'swift-clients/Package.swift')
      await utimes(source, 100, 100)
      await assert.rejects(restoreState(f), /identity|metadata/u)
      assert.equal((await stat(source)).mtimeMs, 100000)
      await assert.rejects(lstat(join(f.packageRoot, '.build')), { code: 'ENOENT' })
    })
  }
})

test('explicit role CLI round-trips without selecting a shared Gradle cache', async t => {
  const f = await fixture(t)
  const checkout = join(f.root, 'candidate-clients')
  await rename(f.checkoutRoot, checkout)
  const packageRoot = join(checkout, 'swift-clients/apps/android')
  const wrapper = join(packageRoot, 'Android/gradle/wrapper')
  await mkdir(wrapper, { recursive: true })
  await writeFile(
    join(wrapper, 'gradle-wrapper.properties'),
    'distributionUrl=https://services.gradle.org/distributions/gradle-9.4.1-bin.zip\n',
  )
  const bin = join(f.root, 'bin')
  await mkdir(bin)
  await writeFile(join(bin, 'uname'), '#!/bin/sh\nexit 99\n')
  await chmod(join(bin, 'uname'), 0o755)
  const env = {
    ...process.env,
    PATH: `${bin}:${process.env.PATH}`,
    GITHUB_WORKSPACE: f.root,
    CANDIDATE_REVISION: f.identity.revision,
    GITHUB_RUN_ID: f.identity.run,
    GITHUB_RUN_ATTEMPT: f.identity.attempt,
  }
  const cli = 'swift-clients/apps/android/tooling/android-state-cli.mjs'
  function run(command, role = 'test-runner') {
    return spawnSync(
      process.execPath,
      [cli, command, packageRoot, f.artifactRoot, '--role', role],
      { env, encoding: 'utf8' },
    )
  }
  const packed = run('pack')
  assert.equal(packed.status, 0, packed.stderr)
  await rm(join(packageRoot, '.build'), { recursive: true })
  const restored = run('restore')
  assert.equal(restored.status, 0, restored.stderr)
  assert.match(restored.stdout, /"role":"test-runner"/u)
  assert.notEqual(run('restore', 'unknown').status, 0)
  assert.equal(await readFile(join(f.cacheRoot, 'cache-entry'), 'utf8'), 'cache')
})

test('only the exact runner-role debug alias is admitted; native policy stays unchanged', () => {
  assert.doesNotThrow(() => assertDebugLink('.build/debug', testRunnerDebugTarget, 'test-runner'))
  assert.doesNotThrow(() => assertDebugLink('.build/debug', 'arm64-apple-macosx/debug'))
  assert.throws(
    () => assertDebugLink('.build/debug', testRunnerDebugTarget),
    /Unsafe Android state symlink/u,
  )
  for (const target of [
    'out/Products/Debug-android-x86_64',
    '../out/Products/Debug-android-aarch64',
    '/out/Products/Debug-android-aarch64',
    'arm64-apple-macosx/debug',
  ]) {
    assert.throws(
      () => assertDebugLink('.build/debug', target, 'test-runner'),
      /Unsafe Android state symlink/u,
    )
  }
  assert.throws(
    () => assertDebugLink('.build/misplaced', testRunnerDebugTarget, 'test-runner'),
    /Unsafe Android state symlink/u,
  )
})

test('producer rejects every symlinked product ancestor without following it', async t => {
  for (const suffix of [
    '.build',
    '.build/out',
    '.build/out/Products',
    '.build/out/Products/Debug-android-aarch64',
  ]) {
    await t.test(suffix, async sub => {
      const f = await fixture(sub)
      const foreign = join(f.root, 'foreign')
      const path = join(f.packageRoot, suffix)
      await rename(path, foreign)
      await symlink(foreign, path)
      await writeFile(join(foreign, 'sentinel'), 'foreign')
      await assert.rejects(packState(f), /ancestor must be a real directory/u)
      assert.equal(await readFile(join(foreign, 'sentinel'), 'utf8'), 'foreign')
    })
  }
})

test('crafted consumer parent links reject before tracked timestamps or destination writes', async t => {
  for (const path of [
    '.build/out',
    '.build/out/Products',
    '.build/out/Products/Debug-android-aarch64',
  ]) {
    await t.test(path, async sub => {
      const f = await fixture(sub)
      await packState(f)
      await rm(join(f.packageRoot, '.build'), { recursive: true })
      const foreign = join(f.root, 'foreign')
      await mkdir(foreign)
      await writeFile(join(foreign, 'sentinel'), 'foreign')
      await rewrite(
        f,
        header => {
          header.type = 'symlink'
          header.target = foreign
          header.mode = 0
          header.mtimeNs = null
        },
        path,
      )
      const source = join(f.checkoutRoot, 'swift-clients/Package.swift')
      await utimes(source, 100, 100)
      await assert.rejects(restoreState(f), /ENOENT|not a directory|real directory/u)
      assert.equal((await stat(source)).mtimeMs, 100000)
      assert.equal(await readFile(join(foreign, 'sentinel'), 'utf8'), 'foreign')
      await assert.rejects(lstat(join(f.packageRoot, '.build')), { code: 'ENOENT' })
    })
  }
})

test('runner product must be a regular executable in producer and staged consumer', async t => {
  for (const kind of ['source-symlink', 'source-mode', 'staged-mode']) {
    await t.test(kind, async sub => {
      const f = await fixture(sub)
      const runner = join(f.products, 'VouchaAndroidTests-test-runner')
      if (kind === 'staged-mode') {
        await packState(f)
        await rm(join(f.packageRoot, '.build'), { recursive: true })
        await rewrite(
          f,
          header => {
            header.mode = 0o644
          },
          '.build/out/Products/Debug-android-aarch64/VouchaAndroidTests-test-runner',
        )
        await assert.rejects(restoreState(f), /regular executable/u)
        await assert.rejects(lstat(join(f.packageRoot, '.build')), { code: 'ENOENT' })
      } else {
        if (kind === 'source-mode') await chmod(runner, 0o644)
        else {
          await rm(runner)
          await writeFile(join(f.root, 'foreign-runner'), 'foreign')
          await chmod(join(f.root, 'foreign-runner'), 0o755)
          await symlink(join(f.root, 'foreign-runner'), runner)
        }
        await assert.rejects(packState(f), /regular executable/u)
      }
    })
  }
})
