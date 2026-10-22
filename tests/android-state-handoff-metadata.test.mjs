import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { readFile, stat, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import test from 'node:test'
import { gunzipSync, gzipSync } from 'node:zlib'
import {
  packState,
  restoreState,
} from '../swift-clients/apps/android/tooling/android-state-handoff.mjs'
import { androidStateFixture } from './android-state-handoff-fixture.mjs'

const limits = {
  entries: 100,
  unpackedBytes: 1024,
  compressedBytes: 1024,
  scanMs: 5_000,
  transferMs: 5_000,
}

async function replaceArchive(state, plain) {
  const archive = gzipSync(plain)
  await writeFile(join(state.artifactRoot, 'android-state.gz'), archive)
  const manifestPath = join(state.artifactRoot, 'android-state.json')
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'))
  manifest.compressedBytes = archive.length
  manifest.sha256 = createHash('sha256').update(archive).digest('hex')
  await writeFile(manifestPath, `${JSON.stringify(manifest)}\n`)
}

function metadataMember(plain) {
  let offset = 5
  offset += 4 + plain.readUInt32BE(offset) // exact archive identity
  while (offset < plain.length) {
    const length = plain.readUInt32BE(offset)
    if (length === 0) break
    const member = JSON.parse(plain.subarray(offset + 4, offset + 4 + length).toString())
    const end =
      offset +
      4 +
      length +
      (member.type === 'file' || member.type === 'source-metadata' ? member.size : 0)
    if (member.type === 'source-metadata') return { start: offset, end }
    offset = end
  }
  throw new Error('source metadata member missing')
}

test('rejects rehashed forged source metadata before changing source or generated state', async t => {
  const state = await androidStateFixture(t)
  await packState({ ...state, limits })
  const source = join(state.checkoutRoot, 'swift-clients/Package.swift')
  const before = (await stat(source, { bigint: true })).mtimeNs
  const plain = gunzipSync(await readFile(join(state.artifactRoot, 'android-state.gz')))
  const marker = Buffer.from('"sha256":"')
  const offset = plain.indexOf(marker)
  assert.ok(offset > 0, 'source metadata hash missing')
  plain[offset + marker.length] = plain[offset + marker.length] === 0x61 ? 0x62 : 0x61
  await replaceArchive(state, plain)
  await assert.rejects(restoreState({ ...state, limits }), /source bytes differ from producer/u)
  assert.equal((await stat(source, { bigint: true })).mtimeNs, before)
  assert.equal(
    await readFile(join(state.packageRoot, '.build/plugins/outputs/native.o'), 'utf8'),
    'native',
  )
})

test('rejects rehashed archive missing its required source metadata member', async t => {
  const state = await androidStateFixture(t)
  await packState({ ...state, limits })
  const archivePath = join(state.artifactRoot, 'android-state.gz')
  const plain = gunzipSync(await readFile(archivePath))
  const { start, end } = metadataMember(plain)
  await replaceArchive(state, Buffer.concat([plain.subarray(0, start), plain.subarray(end)]))
  await assert.rejects(restoreState({ ...state, limits }), /unpacked inventory mismatch/u)
})

test('rejects a member after the final source metadata member', async t => {
  const state = await androidStateFixture(t)
  await packState({ ...state, limits })
  const plain = gunzipSync(await readFile(join(state.artifactRoot, 'android-state.gz')))
  const { end } = metadataMember(plain)
  const header = Buffer.from(
    JSON.stringify({
      path: 'source-input-metadata/extra',
      type: 'directory',
      size: 0,
      mode: 0o700,
    }),
  )
  const length = Buffer.alloc(4)
  length.writeUInt32BE(header.length)
  await replaceArchive(
    state,
    Buffer.concat([plain.subarray(0, end), length, header, plain.subarray(end)]),
  )
  await assert.rejects(restoreState({ ...state, limits }), /member follows source metadata/u)
})
