import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdir, mkdtemp, readFile, rm, stat, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  inventoryTrackedSourceMetadata,
  restoreTrackedSourceTimes,
} from '../swift-clients/apps/android/tooling/android-state-source-metadata.mjs'

function git(root, ...args) {
  const result = spawnSync('git', args, { cwd: root, encoding: 'utf8' })
  assert.equal(result.status, 0, result.stderr)
  return result.stdout.trim()
}
function exactTime(path, ns) {
  const result = spawnSync(
    'python3',
    ['-I', '-c', 'import os,sys; n=int(sys.argv[2]); os.utime(sys.argv[1], ns=(n,n))', path, ns],
    { encoding: 'utf8' },
  )
  assert.equal(result.status, 0, result.stderr)
}
function exactAccessAndModificationTime(path, accessNs, modificationNs) {
  const result = spawnSync(
    'python3',
    [
      '-I',
      '-c',
      'import os,sys; os.utime(sys.argv[1], ns=(int(sys.argv[2]),int(sys.argv[3])))',
      path,
      accessNs,
      modificationNs,
    ],
    { encoding: 'utf8' },
  )
  assert.equal(result.status, 0, result.stderr)
}
async function fixture(t) {
  const checkoutRoot = await mkdtemp(join(tmpdir(), 'android-source-metadata-'))
  t.after(() => rm(checkoutRoot, { recursive: true, force: true }))
  git(checkoutRoot, 'init', '-q')
  const first = join(checkoutRoot, 'swift-clients/core/Sources/a.swift')
  const second = join(checkoutRoot, 'swift-clients/apps/android/Package.swift')
  await mkdir(join(checkoutRoot, 'swift-clients/core/Sources'), { recursive: true })
  await mkdir(join(checkoutRoot, 'swift-clients/apps/android'), { recursive: true })
  await writeFile(first, 'public let a = 1\n')
  await writeFile(second, '// package\n')
  git(checkoutRoot, 'add', 'swift-clients')
  git(
    checkoutRoot,
    '-c',
    'user.name=Test',
    '-c',
    'user.email=test@example.invalid',
    'commit',
    '-qm',
    'fixture',
  )
  const identity = {
    revision: git(checkoutRoot, 'rev-parse', 'HEAD'),
    run: '123',
    attempt: '2',
    gradle: '9.4.1',
  }
  const limits = { entries: 20, bytes: 8192, metadataBytes: 8192, scanMs: 5000 }
  return { checkoutRoot, first, second, identity, limits }
}

test('restores exact source nanoseconds only after identical tracked bytes', async t => {
  const state = await fixture(t)
  exactTime(state.first, '1792634209267950316')
  exactTime(state.second, '1792634209267950317')
  const metadata = await inventoryTrackedSourceMetadata(state)
  assert.deepEqual(
    metadata.entries.map(entry => entry.path),
    ['swift-clients/apps/android/Package.swift', 'swift-clients/core/Sources/a.swift'],
  )
  assert.equal(metadata.entries[1].mtimeNs, '1792634209267950316')
  exactAccessAndModificationTime(state.first, '2000000000000000000', '1792634209267950416')
  exactTime(state.second, '1792634209267950417')
  await restoreTrackedSourceTimes({ ...state, metadata })
  assert.equal(
    (await stat(state.first, { bigint: true })).mtimeNs.toString(),
    '1792634209267950316',
  )
  assert.equal(
    (await stat(state.first, { bigint: true })).atimeNs.toString(),
    '2000000000000000000',
  )
  assert.equal(
    (await stat(state.second, { bigint: true })).mtimeNs.toString(),
    '1792634209267950317',
  )
})

test('changed later file fails before earlier source mtime is modified', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  exactTime(state.second, '1792634209267950417')
  await writeFile(state.first, 'public let a = 2\n')
  const before = (await stat(state.second, { bigint: true })).mtimeNs
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /bytes differ/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, before)
})

test('missing and newly tracked inputs invalidate the complete inventory', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  await rm(state.first)
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata }),
    /No such file or directory/,
  )
  await writeFile(state.first, 'public let a = 1\n')
  const extra = join(state.checkoutRoot, 'swift-clients/ui/Sources/extra.swift')
  await mkdir(join(state.checkoutRoot, 'swift-clients/ui/Sources'), { recursive: true })
  await writeFile(extra, 'public let extra = 1\n')
  git(state.checkoutRoot, 'add', 'swift-clients/ui/Sources/extra.swift')
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /source set differs/)
})

test('rejects symlinked tracked file or ancestor and leaves source alone', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  exactTime(state.second, '1792634209267950417')
  const untouched = (await stat(state.second, { bigint: true })).mtimeNs
  const outside = join(state.checkoutRoot, 'outside.swift')
  await writeFile(outside, await readFile(state.first))
  await rm(state.first)
  await symlink(outside, state.first)
  await assert.rejects(restoreTrackedSourceTimes({ ...state, metadata }), /symbolic links/)
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, untouched)
  await rm(state.first)
  await writeFile(state.first, 'public let a = 1\n')
  const sourceDirectory = join(state.checkoutRoot, 'swift-clients/core/Sources')
  const moved = join(state.checkoutRoot, 'source-moved')
  const { rename } = await import('node:fs/promises')
  await rename(sourceDirectory, moved)
  await symlink(moved, sourceDirectory)
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata }),
    /symbolic links|Not a directory/,
  )
  assert.equal((await stat(state.second, { bigint: true })).mtimeNs, untouched)
})

test('rejects identity, malformed metadata, and caps before touching files', async t => {
  const state = await fixture(t)
  const metadata = await inventoryTrackedSourceMetadata(state)
  const before = (await stat(state.first, { bigint: true })).mtimeNs
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, identity: { ...state.identity, run: '4' }, metadata }),
    /identity/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({
      ...state,
      metadata: {
        ...metadata,
        entries: [{ ...metadata.entries[0], mtimeNs: '01' }, ...metadata.entries.slice(1)],
      },
    }),
    /invalid metadata value/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({
      ...state,
      metadata: {
        ...metadata,
        entries: [{ ...metadata.entries[0], mtimeNs: 0 }, ...metadata.entries.slice(1)],
      },
    }),
    /invalid metadata value/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({
      ...state,
      metadata: { ...metadata, entries: [metadata.entries[0], metadata.entries[0]] },
    }),
    /duplicated or unordered/,
  )
  await assert.rejects(
    restoreTrackedSourceTimes({ ...state, metadata, limits: { ...state.limits, entries: 1 } }),
    /cap|bounds/,
  )
  assert.equal((await stat(state.first, { bigint: true })).mtimeNs, before)
})
