import { createHash } from 'node:crypto'
import { gunzipSync } from 'node:zlib'

function fail(message) {
  throw new Error(`Exporter package archive rejected: ${message}`)
}

function octal(buffer) {
  const text = buffer.toString('ascii').replace(/\0.*$/u, '').trim()
  if (!/^[0-7]+$/u.test(text)) fail('archive contains a non-octal numeric field')
  return Number.parseInt(text, 8)
}

export function packageIntegrity(bytes) {
  return `sha512-${createHash('sha512').update(bytes).digest('base64')}`
}

export function verifyPackageIntegrity(bytes, integrity) {
  if (!Buffer.isBuffer(bytes) || typeof integrity !== 'string')
    fail('invalid package integrity input')
  if (packageIntegrity(bytes) !== integrity) fail('package archive integrity mismatch')
  return true
}

export function inspectPackageArchive(bytes, maximumExpandedBytes = 32 * 1024 * 1024) {
  if (!Buffer.isBuffer(bytes) || bytes.length === 0) fail('archive is empty')
  let uncompressed
  try {
    uncompressed = gunzipSync(bytes, { maxOutputLength: maximumExpandedBytes })
  } catch (error) {
    fail(`archive cannot be safely decompressed: ${error.message}`)
  }
  const entries = []
  let offset = 0
  let expandedBytes = 0
  const names = new Set()
  while (offset + 512 <= uncompressed.length) {
    const header = uncompressed.subarray(offset, offset + 512)
    if (header.every(byte => byte === 0)) break
    let name = header.subarray(0, 100).toString('utf8').replace(/\0.*$/u, '')
    const prefix = header.subarray(345, 500).toString('utf8').replace(/\0.*$/u, '')
    if (prefix) name = `${prefix}/${name}`
    const type = String.fromCharCode(header[156] || 48)
    const size = octal(header.subarray(124, 136))
    const canonicalName = name.replace(/\/$/u, '')
    if (
      name.startsWith('/') ||
      name.includes('\\') ||
      canonicalName.split('/').some(part => part === '..' || part === '.' || part === '') ||
      canonicalName.split('/')[0] !== 'package'
    )
      fail('archive contains a noncanonical member path')
    if (type !== '0' && type !== '5') fail('archive contains a link or special file')
    if (names.has(canonicalName)) fail('archive contains duplicate members')
    names.add(canonicalName)
    if (size > maximumExpandedBytes) fail('archive member exceeds the expanded size limit')
    expandedBytes += size
    if (expandedBytes > maximumExpandedBytes) fail('archive exceeds the expanded size limit')
    const dataStart = offset + 512
    const dataEnd = dataStart + size
    if (dataEnd > uncompressed.length) fail('archive is truncated')
    entries.push({ name: canonicalName, type, bytes: uncompressed.subarray(dataStart, dataEnd) })
    offset = dataStart + Math.ceil(size / 512) * 512
  }
  if (entries.length === 0) fail('archive contains no members')
  return { entries, expandedBytes }
}
