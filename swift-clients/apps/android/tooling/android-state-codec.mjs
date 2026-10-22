import { createHash } from 'node:crypto'
import { createReadStream, lstatSync } from 'node:fs'

export const magic = Buffer.from('VACS1')

function encodedHeader(value) {
  const bytes = Buffer.from(JSON.stringify(value))
  if (bytes.length > 8192) throw new Error('Android state header exceeds 8 KiB')
  const length = Buffer.alloc(4)
  length.writeUInt32BE(bytes.length)
  return [length, bytes]
}

export async function* archiveChunks(entries, identity, deadline) {
  yield magic
  yield* encodedHeader(identity)
  for (const entry of entries) {
    if (Date.now() > deadline) throw new Error('Android state compression timed out')
    yield* encodedHeader({
      path: entry.path,
      type: entry.type,
      size: entry.size ?? 0,
      mode: entry.mode,
      mtimeNs: entry.mtimeNs ?? null,
      target: entry.target ?? null,
    })
    if (entry.type === 'file') {
      let actual = 0
      for await (const chunk of createReadStream(entry.source, { highWaterMark: 64 * 1024 })) {
        actual += chunk.length
        if (actual > entry.size || Date.now() > deadline) {
          throw new Error(`Android state changed or compression timed out: ${entry.path}`)
        }
        yield chunk
      }
      if (actual !== entry.size) throw new Error(`Android state changed: ${entry.path}`)
      const after = lstatSync(entry.source, { bigint: true })
      if (!after.isFile() || after.size !== BigInt(entry.size) ||
          after.mtimeNs.toString() !== entry.mtimeNs) {
        throw new Error(`Android state changed during compression: ${entry.path}`)
      }
    }
  }
  yield Buffer.alloc(4)
}

export class ByteReader {
  constructor(stream, deadline, cap) {
    this.iterator = stream[Symbol.asyncIterator]()
    this.buffer = Buffer.alloc(0)
    this.deadline = deadline
    this.cap = cap
    this.readBytes = 0
  }

  async read(length) {
    if (length < 0 || length > 64 * 1024) throw new Error('Invalid Android state read length')
    const chunks = []
    let remaining = length
    while (remaining) {
      if (Date.now() > this.deadline) throw new Error('Android state extraction timed out')
      if (!this.buffer.length) {
        const next = await this.iterator.next()
        if (next.done) throw new Error('Android state archive ended early')
        this.buffer = next.value
      }
      const take = Math.min(remaining, this.buffer.length)
      chunks.push(this.buffer.subarray(0, take))
      this.buffer = this.buffer.subarray(take)
      remaining -= take
    }
    this.readBytes += length
    if (this.readBytes > this.cap) throw new Error('Android state exceeded unpacked-byte cap')
    return Buffer.concat(chunks, length)
  }

  async header() {
    const length = (await this.read(4)).readUInt32BE()
    if (!length) return null
    if (length > 8192) throw new Error('Android state header exceeds 8 KiB')
    return JSON.parse((await this.read(length)).toString('utf8'))
  }

  async end() {
    if (this.buffer.length || !(await this.iterator.next()).done) {
      throw new Error('Android state archive has trailing bytes')
    }
  }
}

export async function sha256File(path, limit, deadline) {
  const digest = createHash('sha256')
  let bytes = 0
  for await (const chunk of createReadStream(path)) {
    bytes += chunk.length
    if (bytes > limit || Date.now() > deadline) throw new Error('Android state hash exceeded cap')
    digest.update(chunk)
  }
  return { digest: digest.digest('hex'), bytes }
}
