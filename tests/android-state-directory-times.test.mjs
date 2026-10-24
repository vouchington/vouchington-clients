import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { chmod, mkdir, readFile, stat, symlink, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { gunzipSync, gzipSync } from 'node:zlib'
import { archiveChunks } from '../swift-clients/apps/android/tooling/android-state-codec.mjs'
import {
  inventoryState,
  packState,
  restoreState,
} from '../swift-clients/apps/android/tooling/android-state-handoff.mjs'
import { androidStateFixture as fixture } from './android-state-handoff-fixture.mjs'

const limits = {
  entries: 100,
  unpackedBytes: 1024,
  compressedBytes: 1024,
  scanMs: 5_000,
  transferMs: 5_000,
}

function childDigest(children) {
  const hash = createHash('sha256')
  for (const child of [...children].sort((a, b) =>
    Buffer.compare(Buffer.from(a.name), Buffer.from(b.name)),
  )) {
    hash.update(child.type)
    hash.update(Buffer.from([0]))
    hash.update(child.name)
    hash.update(Buffer.from([0]))
  }
  return hash.digest('hex')
}

function directoryRecord(path, mtimeNs, children = [], mode = 0o755) {
  return { path, mtimeNs, mode, children: children.length, childrenSha256: childDigest(children) }
}

function runDirectoryHelper(cwd, records) {
  const helper = fileURLToPath(
    new URL('../swift-clients/apps/android/tooling/android-state-dir-ns.py', import.meta.url),
  )
  return spawnSync('python3', ['-I', helper], {
    cwd,
    input: `${records.map(record => JSON.stringify(record)).join('\n')}\n`,
    encoding: 'utf8',
  })
}

test('directory timestamp helper validates all child sets before changing any mtime', async t => {
  const state = await fixture(t)
  const root = join(state.root, '.build')
  const first = join(root, 'first')
  const last = join(root, 'last')
  await mkdir(first, { recursive: true })
  await mkdir(last)
  await writeFile(join(first, 'source.swift'), 'source')
  await writeFile(join(last, 'source.swift'), 'source')
  await chmod(root, 0o755)
  const originalAtime = '1792634208267950316'
  const setAtime = spawnSync(
    'python3',
    [
      '-I',
      '-c',
      'import os,sys; s=os.stat(sys.argv[1]); os.utime(sys.argv[1], ns=(int(sys.argv[2]),s.st_mtime_ns))',
      first,
      originalAtime,
    ],
    { encoding: 'utf8' },
  )
  assert.equal(setAtime.status, 0, setAtime.stderr)
  const before = (await stat(first, { bigint: true })).mtimeNs
  const wanted = '1792634209267950316'
  const records = [
    directoryRecord('.build', wanted, [
      { name: 'first', type: 'directory' },
      { name: 'last', type: 'directory' },
    ]),
    directoryRecord('.build/first', wanted, [{ name: 'source.swift', type: 'file' }]),
    directoryRecord('.build/last', wanted, [{ name: 'source.swift', type: 'file' }]),
  ]
  const mismatch = runDirectoryHelper(state.root, [
    ...records.slice(0, 2),
    directoryRecord('.build/last', wanted, [{ name: 'source.swift', type: 'directory' }]),
  ])
  assert.notEqual(mismatch.status, 0)
  assert.match(mismatch.stderr, /graph or mode differs/u)
  assert.equal((await stat(first, { bigint: true })).mtimeNs, before)
  const resetAtime = spawnSync(
    'python3',
    [
      '-I',
      '-c',
      'import os,sys; s=os.stat(sys.argv[1]); os.utime(sys.argv[1], ns=(int(sys.argv[2]),s.st_mtime_ns))',
      first,
      originalAtime,
    ],
    { encoding: 'utf8' },
  )
  assert.equal(resetAtime.status, 0, resetAtime.stderr)
  const success = runDirectoryHelper(state.root, records)
  assert.equal(success.status, 0, success.stderr)
  assert.equal((await stat(root, { bigint: true })).mtimeNs.toString(), wanted)
  assert.equal((await stat(first, { bigint: true })).mtimeNs.toString(), wanted)
  assert.equal((await stat(first, { bigint: true })).atimeNs.toString(), originalAtime)
  assert.equal((await stat(last, { bigint: true })).mtimeNs.toString(), wanted)
})

test('directory timestamp helper rejects missing, extra, unsafe and mode-changed members', async t => {
  const state = await fixture(t)
  const root = join(state.root, '.build')
  await mkdir(join(root, 'child'), { recursive: true })
  await chmod(root, 0o755)
  const wanted = '1792634209267950316'
  const child = directoryRecord('.build/child', wanted)
  const validRoot = directoryRecord('.build', wanted, [{ name: 'child', type: 'directory' }])
  const original = (await stat(root, { bigint: true })).mtimeNs
  for (const records of [
    [directoryRecord('.build', wanted), child],
    [validRoot, directoryRecord('.build/child', wanted, [{ name: 'missing', type: 'file' }])],
    [validRoot, { ...child, mode: 0o700 }],
    [validRoot, child, child],
    [validRoot, { ...child, path: '.build/../child' }],
  ]) {
    const result = runDirectoryHelper(state.root, records)
    assert.notEqual(result.status, 0)
    assert.equal((await stat(root, { bigint: true })).mtimeNs, original)
  }
  await mkdir(join(state.root, 'outside'))
  await symlink(join(state.root, 'outside'), join(root, 'link'))
  const afterLink = (await stat(root, { bigint: true })).mtimeNs
  const linkedRoot = directoryRecord('.build', wanted, [
    { name: 'child', type: 'directory' },
    { name: 'link', type: 'symlink' },
  ])
  const linked = runDirectoryHelper(state.root, [
    linkedRoot,
    child,
    directoryRecord('.build/link', wanted),
  ])
  assert.notEqual(linked.status, 0)
  assert.equal((await stat(root, { bigint: true })).mtimeNs, afterLink)
})

test('packing rejects a new unarchived child even when existing files are unchanged', async t => {
  const state = await fixture(t)
  const inventory = inventoryState(state.packageRoot, state.cacheRoot, state.checkoutRoot, limits)
  await writeFile(join(state.packageRoot, '.build/plugins/outputs/unarchived'), 'extra')
  await assert.rejects(async () => {
    for await (const _chunk of archiveChunks(
      inventory.entries,
      state.identity,
      Date.now() + 5_000,
      Buffer.from('{}'),
    )) {
      /* consume the bounded archive */
    }
  }, /directory changed during compression|directory membership changed/u)
})

test('format 3 and malformed directory timestamps are rejected before state replacement', async t => {
  const state = await fixture(t)
  await packState({ ...state, limits })
  const manifestPath = join(state.artifactRoot, 'android-state.json')
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'))
  await writeFile(manifestPath, `${JSON.stringify({ ...manifest, format: 3 })}\n`)
  await assert.rejects(restoreState({ ...state, limits }), /manifest identity or limits mismatch/u)
  await writeFile(manifestPath, `${JSON.stringify(manifest)}\n`)
  const archivePath = join(state.artifactRoot, 'android-state.gz')
  const original = gunzipSync(await readFile(archivePath))
  let offset = 5
  const chunks = [original.subarray(0, offset)]
  let changed = false
  while (offset < original.length) {
    const length = original.readUInt32BE(offset)
    if (length === 0) {
      chunks.push(original.subarray(offset))
      break
    }
    const header = JSON.parse(original.subarray(offset + 4, offset + 4 + length).toString())
    const next =
      offset +
      4 +
      length +
      (header.type === 'file' || header.type === 'source-metadata' ? header.size : 0)
    if (!changed && header.path === '.build') {
      header.mtimeNs = '0123456789012345678'
      const bytes = Buffer.from(JSON.stringify(header))
      const size = Buffer.alloc(4)
      size.writeUInt32BE(bytes.length)
      chunks.push(size, bytes)
      changed = true
    } else chunks.push(original.subarray(offset, next))
    offset = next
  }
  assert.equal(changed, true)
  const forged = gzipSync(Buffer.concat(chunks))
  await writeFile(archivePath, forged)
  await writeFile(
    manifestPath,
    `${JSON.stringify({
      ...manifest,
      compressedBytes: forged.length,
      sha256: createHash('sha256').update(forged).digest('hex'),
    })}\n`,
  )
  await assert.rejects(restoreState({ ...state, limits }), /member type invalid/u)
  assert.equal(
    await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
})
