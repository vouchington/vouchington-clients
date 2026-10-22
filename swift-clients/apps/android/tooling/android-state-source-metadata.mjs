import { spawnSync } from 'node:child_process'
import { lstatSync, realpathSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { assertCandidate, assertIdentity } from './android-state-inventory.mjs'

export const sourceMetadataLimits = Object.freeze({
  entries: 10_000,
  bytes: 512 * 1024 * 1024,
  metadataBytes: 4 * 1024 * 1024,
  scanMs: 60_000,
})
const maxTimestampNs = 9_223_372_036_854_775_807n
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
function scanWithNoFollowDescriptors(root, paths, deadline, limits) {
  const input = JSON.stringify(paths)
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
  if (metadata?.version !== 1 || JSON.stringify(metadata.identity) !== JSON.stringify(identity) ||
      !Array.isArray(metadata.entries) || metadata.entries.length > limits.entries ||
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
}

export async function inventoryTrackedSourceMetadata({ checkoutRoot, identity, limits = sourceMetadataLimits }) {
  const root = validatedContext(checkoutRoot, identity, limits)
  const deadline = Date.now() + limits.scanMs
  const paths = trackedPaths(root, limits)
  const entries = scanWithNoFollowDescriptors(root, paths, deadline, limits)
  const metadata = { version: 1, identity, entries }
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
  const input = JSON.stringify(metadata.entries)
  if (Buffer.byteLength(input) > limits.metadataBytes || Date.now() >= deadline) fail('timestamp replay exceeded bounds')
  const result = spawnSync('python3', ['-I', fileURLToPath(new URL('./android-state-source-ns.py', import.meta.url))], {
    cwd: root, input, encoding: 'utf8', timeout: deadline - Date.now(), maxBuffer: 8192,
  })
  if (result.error || result.status !== 0) fail(`exact timestamp replay failed: ${result.error?.message ?? result.stderr.trim()}`)
  return { entries: metadata.entries.length }
}
