import { createHash } from 'node:crypto'
import { closeSync, constants, fstatSync, lstatSync, openSync, opendirSync, readSync, writeFileSync } from 'node:fs'
import { dirname, isAbsolute, join, resolve } from 'node:path'

export const entryName = /^[a-f0-9]{32}$/u
const metadata = new Set(['gc.properties', 'gradle-build-cache.lock'])
export const cacheLimits = { entries: 2_000, bytes: 512 * 1024 ** 2, manifestBytes: 1024 ** 2, ms: 30_000 }

export function checkTime(deadline) {
  if (Date.now() > deadline) throw Error('Kotlin cache time limit exceeded')
}

export function safeDirectory(path) {
  if (!isAbsolute(path) || resolve(path) !== path || dirname(path) === path) throw Error('Unsafe Kotlin cache directory')
  for (let current = path; ; current = dirname(current)) {
    const stat = lstatSync(current)
    if (!stat.isDirectory() || stat.isSymbolicLink()) throw Error('Unsafe Kotlin cache directory ancestor')
    if (dirname(current) === current) break
  }
}

export function fileDigest(path, maximum, deadline, destination, capture = false) {
  const input = openSync(path, constants.O_RDONLY | constants.O_NOFOLLOW | constants.O_NONBLOCK)
  let output
  try {
    const before = fstatSync(input)
    if (!before.isFile() || before.size > maximum) throw Error('Unsafe or oversized Kotlin cache file')
    if (destination) output = openSync(destination, 'wx', 0o600)
    const hash = createHash('sha256')
    const chunks = []
    const buffer = Buffer.alloc(65536)
    let bytes = 0
    for (;;) {
      checkTime(deadline)
      const count = readSync(input, buffer, 0, buffer.length)
      if (!count) break
      bytes += count
      if (bytes > maximum) throw Error('Kotlin cache byte limit exceeded')
      const chunk = buffer.subarray(0, count)
      hash.update(chunk)
      if (capture) chunks.push(Buffer.from(chunk))
      if (output !== undefined) writeFileSync(output, chunk)
    }
    const after = fstatSync(input)
    if (before.size !== bytes || before.size !== after.size || before.mtimeMs !== after.mtimeMs) throw Error('Kotlin cache file changed during read')
    return { size: bytes, sha256: hash.digest('hex'), ...(capture ? { text: Buffer.concat(chunks).toString('utf8') } : {}) }
  } finally {
    closeSync(input)
    if (output !== undefined) closeSync(output)
  }
}

export function inventory(root, limits, deadline, ignoredNames = metadata) {
  safeDirectory(root)
  const entries = []
  let count = 0, bytes = 0
  const directory = opendirSync(root)
  try {
    let member
    while ((member = directory.readSync()) !== null) {
      checkTime(deadline)
      const path = join(root, member.name)
      const stat = lstatSync(path)
      if (!stat.isFile() || stat.isSymbolicLink()) throw Error('Kotlin cache requires flat regular files')
      bytes += stat.size
      if (bytes > limits.bytes) throw Error('Kotlin cache byte limit exceeded')
      if (ignoredNames.has(member.name)) continue
      if (++count > limits.entries) throw Error('Kotlin cache entry limit exceeded')
      if (!entryName.test(member.name)) throw Error('Unknown Kotlin cache entry')
      entries.push({ name: member.name, ...fileDigest(path, stat.size, deadline) })
    }
  } finally { directory.closeSync() }
  return entries.sort((a, b) => a.name.localeCompare(b.name))
}
