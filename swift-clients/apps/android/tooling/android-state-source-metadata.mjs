import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { lstatSync, realpathSync } from 'node:fs'
import { basename, dirname } from 'node:path/posix'
import { fileURLToPath } from 'node:url'
import { assertCandidate, assertIdentity } from './android-state-inventory.mjs'

export const sourceMetadataLimits = Object.freeze({
  entries: 10_000,
  bytes: 512 * 1024 * 1024,
  metadataBytes: 4 * 1024 * 1024,
  scanMs: 60_000,
})
const maxTimestampNs = 9_223_372_036_854_775_807n
const androidSources = 'swift-clients/apps/android/Sources'
const maxSourceDirectories = 256
function fail(message) { throw new Error(`Android source metadata: ${message}`) }
function limitValues(limits) {
  for (const key of ['entries', 'bytes', 'metadataBytes', 'scanMs']) {
    if (!Number.isSafeInteger(limits[key]) || limits[key] <= 0 || limits[key] > sourceMetadataLimits[key]) {
      fail(`invalid ${key} limit`)
    }
  }
}
function checkedPath(path) {
  if (typeof path !== 'string' || path.length > 4096 || !path.startsWith('swift-clients/') ||
      path.includes('\\') || Array.from(path).some(character => {
        const code = character.codePointAt(0)
        return code < 32 || code === 127
      }) || path.split('/').some(part => part === '' || part === '.' || part === '..') ||
      path.split('/').some(part => part === '.build' || part === 'node_modules')) {
    fail(`unsafe tracked path: ${path}`)
  }
}
function trackedPaths(root, limits) {
  const result = spawnSync('git', ['ls-files', '-z', '--', 'swift-clients/'], {
    cwd: root, encoding: 'buffer', maxBuffer: limits.metadataBytes,
  })
  if (result.error || result.status !== 0) fail(`cannot enumerate tracked files: ${result.error?.message ?? result.stderr.toString()}`)
  const data = result.stdout
  if (!data.length || data.at(-1) !== 0) fail('tracked inventory is empty or incomplete')
  const paths = data.subarray(0, -1).toString('utf8').split('\0')
  if (paths.length > limits.entries) fail('tracked file count exceeds cap')
  let previous = ''
  const folded = new Set()
  for (const path of paths) {
    checkedPath(path)
    if (path <= previous || folded.has(path.toLowerCase())) fail('tracked paths are duplicated or unordered')
    previous = path
    folded.add(path.toLowerCase())
  }
  return paths
}
function sourceDirectories(paths) {
  const names = new Map()
  for (const path of paths) {
    if (!path.startsWith(`${androidSources}/`)) continue
    let child = path
    for (let parent = dirname(child); parent === androidSources || parent.startsWith(`${androidSources}/`);
      child = parent, parent = dirname(parent)) {
      const children = names.get(parent) ?? new Map()
      const name = basename(child)
      const type = child === path ? 'file' : 'directory'
      if (children.has(name) && children.get(name) !== type) fail('tracked source child type collision')
      children.set(name, type)
      names.set(parent, children)
    }
  }
  if (names.size > maxSourceDirectories) fail('Android source directory count exceeds cap')
  return [...names].sort(([left], [right]) => Buffer.compare(Buffer.from(left), Buffer.from(right)))
    .map(([path, children]) => ({
      path,
      childrenSha256: createHash('sha256').update(JSON.stringify(
        [...children].sort(([left], [right]) => Buffer.compare(Buffer.from(left), Buffer.from(right))),
      )).digest('hex'),
    }))
}
function scanWithNoFollowDescriptors(root, paths, directories, deadline, limits) {
  const input = JSON.stringify({ files: paths, directories })
  if (Buffer.byteLength(input) > limits.metadataBytes || Date.now() >= deadline) fail('tracked inventory exceeded bounds')
  const result = spawnSync('python3', ['-I', fileURLToPath(new URL('./android-state-source-ns.py', import.meta.url)), 'inventory'], {
    cwd: root, input, encoding: 'utf8', timeout: deadline - Date.now(), maxBuffer: limits.metadataBytes,
  })
  if (result.error || result.status !== 0) fail(`tracked inventory failed: ${result.error?.message ?? result.stderr.trim()}`)
  return JSON.parse(result.stdout)
}
function validatedContext(checkoutRoot, identity, limits) {
  limitValues(limits)
  assertIdentity(identity)
  const root = realpathSync(checkoutRoot)
  if (!lstatSync(root).isDirectory()) fail('checkout root is not a real directory')
  assertCandidate(root, identity)
  return root
}
function checkedMetadata(metadata, identity, limits) {
  if (metadata?.version !== 2 || JSON.stringify(metadata.identity) !== JSON.stringify(identity) ||
      !Array.isArray(metadata.entries) || !Array.isArray(metadata.directories) ||
      metadata.entries.length + metadata.directories.length > limits.entries ||
      metadata.directories.length > maxSourceDirectories ||
      Buffer.byteLength(JSON.stringify(metadata)) > limits.metadataBytes) fail('metadata schema, identity, or bounds mismatch')
  let previous = ''
  let bytes = 0
  const folded = new Set()
  for (const entry of metadata.entries) {
    if (!entry || Object.keys(entry).sort().join(',') !== 'mtimeNs,path,sha256,size') fail('unexpected metadata entry')
    checkedPath(entry.path)
    if (entry.path <= previous || folded.has(entry.path.toLowerCase())) fail('metadata paths duplicated or unordered')
    if (!Number.isSafeInteger(entry.size) || entry.size < 0 || typeof entry.sha256 !== 'string' ||
        !/^[0-9a-f]{64}$/u.test(entry.sha256) || typeof entry.mtimeNs !== 'string' ||
        !/^(?:0|[1-9][0-9]{0,18})$/u.test(entry.mtimeNs) || BigInt(entry.mtimeNs) > maxTimestampNs) {
      fail(`invalid metadata value: ${entry.path}`)
    }
    bytes += entry.size
    if (bytes > limits.bytes) fail('metadata logical bytes exceed cap')
    previous = entry.path
    folded.add(entry.path.toLowerCase())
  }
  const expected = sourceDirectories(metadata.entries.map(entry => entry.path))
  if (expected.length !== metadata.directories.length) fail('source directory set differs from tracked inputs')
  for (let index = 0; index < expected.length; index++) {
    const entry = metadata.directories[index]
    if (!entry || Object.keys(entry).sort().join(',') !== 'childrenSha256,mode,mtimeNs,path' ||
        entry.path !== expected[index].path || entry.childrenSha256 !== expected[index].childrenSha256 ||
        !Number.isInteger(entry.mode) || entry.mode < 0 || entry.mode > 0o777 ||
        typeof entry.mtimeNs !== 'string' || !/^(?:0|[1-9][0-9]{0,18})$/u.test(entry.mtimeNs) ||
        BigInt(entry.mtimeNs) > maxTimestampNs) {
      fail('source directory graph or metadata invalid')
    }
  }
}

export async function inventoryTrackedSourceMetadata({ checkoutRoot, identity, limits = sourceMetadataLimits }) {
  const root = validatedContext(checkoutRoot, identity, limits)
  const deadline = Date.now() + limits.scanMs
  const paths = trackedPaths(root, limits)
  const directories = sourceDirectories(paths)
  if (paths.length + directories.length > limits.entries) fail('source metadata count exceeds cap')
  const scanned = scanWithNoFollowDescriptors(root, paths, directories, deadline, limits)
  const metadata = { version: 2, identity, entries: scanned.entries, directories: scanned.directories }
  checkedMetadata(metadata, identity, limits)
  return metadata
}

export async function restoreTrackedSourceTimes({ checkoutRoot, identity, metadata, limits = sourceMetadataLimits }) {
  const root = validatedContext(checkoutRoot, identity, limits)
  checkedMetadata(metadata, identity, limits)
  const deadline = Date.now() + limits.scanMs
  const paths = trackedPaths(root, limits)
  if (paths.length !== metadata.entries.length || paths.some((path, index) => path !== metadata.entries[index].path)) {
    fail('tracked source set differs from producer')
  }
  // The helper retains no-follow file descriptors across complete byte validation and replay.
  const input = JSON.stringify({ entries: metadata.entries, directories: metadata.directories })
  if (Buffer.byteLength(input) > limits.metadataBytes || Date.now() >= deadline) fail('timestamp replay exceeded bounds')
  const result = spawnSync('python3', ['-I', fileURLToPath(new URL('./android-state-source-ns.py', import.meta.url))], {
    cwd: root, input, encoding: 'utf8', timeout: deadline - Date.now(), maxBuffer: 8192,
  })
  if (result.error || result.status !== 0) fail(`exact timestamp replay failed: ${result.error?.message ?? result.stderr.trim()}`)
  return { entries: metadata.entries.length }
}
