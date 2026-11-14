import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { gzipSync } from 'node:zlib'
import { it } from 'node:test'
import {
  inspectPackageArchive,
  verifyPackageIntegrity,
} from '../scripts/paired-exporter-archive.mjs'

function tarMember(name, { type = '0', contents = Buffer.alloc(0) } = {}) {
  const header = Buffer.alloc(512)
  header.write(name, 0, 100, 'utf8')
  header.write(`${contents.length.toString(8).padStart(11, '0')}\0`, 124, 12, 'ascii')
  header[156] = type.charCodeAt(0)
  const padded = Buffer.alloc(Math.ceil(contents.length / 512) * 512)
  contents.copy(padded)
  return Buffer.concat([header, padded])
}

function tarball(member, extraMembers = []) {
  return gzipSync(Buffer.concat([member, ...extraMembers, Buffer.alloc(1024)]))
}

it('accepts only regular files and directories rooted under package/', () => {
  const result = inspectPackageArchive(
    tarball(tarMember('package/'), [
      tarMember('package/index.js', { contents: Buffer.from('export {}\n') }),
    ]),
  )
  assert.deepEqual(
    result.entries.map(entry => entry.name),
    ['package', 'package/index.js'],
  )
  assert.equal(result.entries[1].bytes.toString(), 'export {}\n')
})

it('rejects archive path traversal, symlink and hardlink members, and oversized expansion', () => {
  assert.throws(
    () => inspectPackageArchive(tarball(tarMember('package/../../escape.js'))),
    /noncanonical member path/u,
  )
  assert.throws(
    () => inspectPackageArchive(tarball(tarMember('package/link', { type: '2' }))),
    /link or special file/u,
  )
  assert.throws(
    () => inspectPackageArchive(tarball(tarMember('package/link', { type: '1' }))),
    /link or special file/u,
  )
  assert.throws(
    () =>
      inspectPackageArchive(
        tarball(tarMember('package/large.bin', { contents: Buffer.alloc(1024) })),
        600,
      ),
    /decompressed|size limit/u,
  )
})

it('checks package bytes against the exact lock SRI', () => {
  const contents = Buffer.from('the reviewed archive bytes')
  const integrity = `sha512-${createHash('sha512').update(contents).digest('base64')}`
  assert.equal(verifyPackageIntegrity(contents, integrity), true)
  assert.throws(() => verifyPackageIntegrity(contents, 'sha512-invalid'), /integrity mismatch/u)
})
