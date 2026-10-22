import assert from 'node:assert/strict'
import {
  chmod,
  mkdir,
  mkdtemp,
  readFile,
  readlink,
  rename,
  rm,
  stat,
  symlink,
  writeFile,
} from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, relative, resolve } from 'node:path'
import test from 'node:test'
import {
  inventoryState,
  packState,
  restoreState,
} from '../swift-clients/apps/android/tooling/android-state-handoff.mjs'

const identity = { revision: 'a'.repeat(40), run: '123', attempt: '2', gradle: '9.4.1' }
const limits = {
  entries: 100,
  unpackedBytes: 1024,
  compressedBytes: 1024,
  scanMs: 5_000,
  transferMs: 5_000,
}

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'android-state-handoff-'))
  t.after(() => rm(root, { recursive: true, force: true }))
  const packageRoot = join(root, 'checkout/swift-clients/apps/android')
  const checkoutRoot = join(root, 'checkout')
  const cacheRoot = join(root, 'cache')
  const artifactRoot = join(root, 'artifact')
  await mkdir(join(packageRoot, '.build/plugins/outputs'), { recursive: true })
  await mkdir(join(packageRoot, 'Android/.gradle'), { recursive: true })
  await mkdir(cacheRoot)
  await writeFile(join(packageRoot, '.build/plugins/outputs/native.o'), 'native')
  await chmod(join(packageRoot, '.build/plugins/outputs/native.o'), 0o755)
  await writeFile(join(packageRoot, 'Android/.gradle/task-state'), 'task-state')
  await writeFile(join(cacheRoot, 'cache-entry'), 'cache')
  return { root, packageRoot, checkoutRoot, cacheRoot, artifactRoot }
}

test('packs and restores exact candidate state with bounded contents', async t => {
  const state = await fixture(t)
  const packed = await packState({ ...state, identity, limits })
  assert.equal(packed.unpackedBytes, 21)
  assert.ok(packed.compressedBytes > 0 && packed.compressedBytes <= limits.compressedBytes)
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await rm(join(state.packageRoot, 'Android/.gradle'), { recursive: true })
  await rm(state.cacheRoot, { recursive: true })
  const restored = await restoreState({ ...state, identity, limits })
  assert.equal(restored.sha256, packed.sha256)
  assert.equal(
    await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
  assert.equal(
    (await stat(join(state.packageRoot, '.build/plugins/outputs/native.o'))).mode & 0o777,
    0o755,
  )
  assert.equal(
    await readFile(join(state.packageRoot, 'Android/.gradle/task-state'), 'utf8'),
    'task-state',
  )
  assert.equal(await readFile(join(state.cacheRoot, 'cache-entry'), 'utf8'), 'cache')
})

test('restores only the internal SwiftPM debug directory alias', async t => {
  const state = await fixture(t)
  await mkdir(join(state.packageRoot, '.build/arm64-apple-macosx/debug'), { recursive: true })
  await symlink('arm64-apple-macosx/debug', join(state.packageRoot, '.build/debug'))
  await packState({ ...state, identity, limits })
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await restoreState({ ...state, identity, limits })
  assert.equal(await readlink(join(state.packageRoot, '.build/debug')), 'arm64-apple-macosx/debug')
})

test('restores a generated Skip native SwiftPM scratch debug alias', async t => {
  const state = await fixture(t)
  const scratch = join(
    state.packageRoot,
    '.build/plugins/outputs/android/VouchaAndroid/destination/skipstone/VouchaAndroid/build/swift',
  )
  await mkdir(join(scratch, 'aarch64-unknown-linux-android/debug'), { recursive: true })
  await symlink('aarch64-unknown-linux-android/debug', join(scratch, 'debug'))
  await packState({ ...state, identity, limits })
  await rm(join(state.packageRoot, '.build'), { recursive: true })
  await restoreState({ ...state, identity, limits })
  assert.equal(await readlink(join(scratch, 'debug')), 'aarch64-unknown-linux-android/debug')
})

test('rebases Skipstone absolute source and package links onto the exact consumer checkout', async t => {
  const producer = await fixture(t)
  const consumer = await fixture(t)
  const swiftFolder =
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain/src/main/swift'
  const producerLinks = join(producer.packageRoot, swiftFolder)
  await mkdir(join(producerLinks, 'Packages'), { recursive: true })
  await mkdir(join(producer.packageRoot, '.build/checkouts/skip-keychain/Sources'), {
    recursive: true,
  })
  await mkdir(join(producer.checkoutRoot, 'swift-clients/core/Sources'), { recursive: true })
  await symlink(
    join(producer.packageRoot, '.build/checkouts/skip-keychain/Sources'),
    join(producerLinks, 'Sources'),
  )
  await symlink(
    join(producer.checkoutRoot, 'swift-clients/core'),
    join(producerLinks, 'Packages/core'),
  )
  await packState({ ...producer, identity, limits: { ...limits, entries: 150 } })
  await mkdir(join(consumer.checkoutRoot, 'swift-clients/core/Sources'), { recursive: true })
  await rm(join(consumer.packageRoot, '.build'), { recursive: true })
  await rm(join(consumer.packageRoot, 'Android/.gradle'), { recursive: true })
  await rm(consumer.cacheRoot, { recursive: true })
  await restoreState({
    ...consumer,
    artifactRoot: producer.artifactRoot,
    identity,
    limits: { ...limits, entries: 150 },
  })
  const links = join(consumer.packageRoot, swiftFolder)
  assert.equal(
    resolve(links, await readlink(join(links, 'Sources'))),
    join(consumer.packageRoot, '.build/checkouts/skip-keychain/Sources'),
  )
  assert.equal(
    resolve(join(links, 'Packages'), await readlink(join(links, 'Packages/core'))),
    join(consumer.checkoutRoot, 'swift-clients/core'),
  )
  assert.equal((await stat(join(links, 'Sources'))).isDirectory(), true)
  assert.equal((await stat(join(links, 'Packages/core'))).isDirectory(), true)
})

test('rejects a Skipstone mirror link outside the candidate checkout', async t => {
  const state = await fixture(t)
  const links = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain/src/main/swift',
  )
  await mkdir(links, { recursive: true })
  await symlink('/etc', join(links, 'Sources'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /mirror target escaped candidate checkout/u,
  )
})

test('rejects a mirrored target that traverses a symlink before dot-dot', async t => {
  const state = await fixture(t)
  const links = join(
    state.packageRoot,
    '.build/plugins/outputs/skip-keychain/SkipKeychain/destination/skipstone/SkipKeychain/src/main/swift',
  )
  const source = join(state.checkoutRoot, 'swift-clients/core')
  const outside = join(state.root, 'outside/deep')
  await mkdir(links, { recursive: true })
  await mkdir(source, { recursive: true })
  await mkdir(outside, { recursive: true })
  await mkdir(join(source, 'bar'))
  await symlink(outside, join(source, 'jump'))
  await symlink(`${relative(links, source)}/jump/../bar`, join(links, 'Sources'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /Unsafe Android state symlink/u,
  )
})

test('rejects an escaping SwiftPM debug alias', async t => {
  const state = await fixture(t)
  await symlink('../../outside/debug', join(state.packageRoot, '.build/debug'))
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /Unsafe Android state symlink/u,
  )
})

test('rejects mismatched attempt, changed archive, external links and over-budget members', async t => {
  const state = await fixture(t)
  assert.throws(
    () =>
      inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, {
        ...limits,
        entries: 2,
      }),
    /member cap/u,
  )
  const linked = join(state.packageRoot, '.build/plugins/outputs/external')
  await symlink('/etc/passwd', linked)
  assert.throws(
    () => inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits),
    /symlink/u,
  )
  await rm(linked)
  await packState({ ...state, identity, limits })
  await assert.rejects(
    restoreState({ ...state, identity: { ...identity, attempt: '3' }, limits }),
    /identity or limits mismatch/u,
  )
  await writeFile(join(state.artifactRoot, 'android-state.gz'), 'forged')
  await assert.rejects(restoreState({ ...state, identity, limits }), /archive size mismatch/u)
})

test('rejects a symlinked destination parent before replacing any generated state', async t => {
  const state = await fixture(t)
  await packState({ ...state, identity, limits })
  const outside = join(state.packageRoot, '..', 'outside')
  await mkdir(join(outside, '.gradle'), { recursive: true })
  await writeFile(join(outside, '.gradle/sentinel'), 'outside')
  await rm(join(state.packageRoot, 'Android'), { recursive: true })
  await symlink(outside, join(state.packageRoot, 'Android'))
  await assert.rejects(
    restoreState({ ...state, identity, limits }),
    /destination parent is unsafe/u,
  )
  assert.equal(await readFile(join(outside, '.gradle/sentinel'), 'utf8'), 'outside')
  assert.equal(
    await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
})

test('rejects a symlinked package ancestor outside the checkout', async t => {
  const state = await fixture(t)
  await packState({ ...state, identity, limits })
  const external = join(state.root, 'outside')
  await mkdir(external)
  const swiftClients = join(state.root, 'checkout/swift-clients')
  await rename(swiftClients, join(external, 'swift-clients'))
  await symlink(join(external, 'swift-clients'), swiftClients)
  await assert.rejects(restoreState({ ...state, identity, limits }), /package ancestor is unsafe/u)
  assert.equal(
    await readFile(
      join(external, 'swift-clients/apps/android/.build/plugins/outputs/native.o'),
      'utf8',
    ),
    'native',
  )
})

test('rejects a relocated package even when its layout looks valid', async t => {
  const state = await fixture(t)
  await packState({ ...state, identity, limits })
  const relocated = join(state.root, 'relocated/swift-clients/apps/android')
  await mkdir(relocated, { recursive: true })
  await assert.rejects(
    restoreState({ ...state, packageRoot: relocated, identity, limits }),
    /Unexpected Android package layout/u,
  )
})
