import assert from 'node:assert/strict'
import {
  chmod,
  mkdir,
  mkdtemp,
  readFile,
  rename,
  rm,
  stat,
  symlink,
  writeFile,
} from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
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
