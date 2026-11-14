import { createHash } from 'node:crypto'
import { spawnSync } from 'node:child_process'
import {
  chmodSync, createReadStream, createWriteStream, existsSync, lstatSync, mkdirSync,
  readFileSync, renameSync, rmSync, statSync, symlinkSync, writeFileSync,
} from 'node:fs'
import { dirname, join, sep } from 'node:path'
import { Readable, Transform } from 'node:stream'
import { pipeline } from 'node:stream/promises'
import { fileURLToPath } from 'node:url'
import { createGunzip, createGzip } from 'node:zlib'
import { archiveChunks, ByteReader, magic, sha256File, sourceMetadataPath } from './android-state-codec.mjs'
import { assertDebugLink, checkedSkipstoneLink, resolveSkipstoneLink } from './android-state-links.mjs'
import { assertIdentity, assertMemberPath, assertPackageLayout, defaultLimits, existingStat, inventoryState, selectedRoots } from './android-state-inventory.mjs'
import { inventoryTrackedSourceMetadata, restoreTrackedSourceTimes, sourceMetadataLimits } from './android-state-source-metadata.mjs'

export { inventoryState } from './android-state-inventory.mjs'

const archiveName = 'android-state.gz'
const manifestName = 'android-state.json'
const maxTimestampNs = 9_223_372_036_854_775_807n
function validTimestamp(value) {
  return typeof value === 'string' && /^(?:0|[1-9][0-9]{0,18})$/u.test(value) &&
    BigInt(value) <= maxTimestampNs
}

function restoreExactFileTimes(staging, entries, deadline) {
  const lines = []
  let inputBytes = 0
  for (const entry of entries) {
    const line = `${JSON.stringify(entry)}\n`
    inputBytes += Buffer.byteLength(line)
    if (inputBytes > 16 * 1024 * 1024 || Date.now() > deadline) {
      throw new Error('Android state timestamp metadata exceeded byte or time cap')
    }
    lines.push(line)
  }
  const remaining = deadline - Date.now()
  if (remaining <= 0) throw new Error('Android state timestamp restoration timed out')
  const result = spawnSync('python3', ['-I', fileURLToPath(new URL('./android-state-ns.py', import.meta.url))], {
    cwd: staging,
    input: lines.join(''),
    encoding: 'utf8',
    timeout: remaining,
    maxBuffer: 8192,
  })
  if (result.error || result.status !== 0) {
    throw new Error(`Android state exact timestamps could not be restored: ${result.error?.message ?? result.stderr.trim()}`)
  }
}

function restoreExactDirectoryTimes(staging, entries, children, deadline) {
  const lines = []
  let inputBytes = 0
  for (const entry of entries) {
    if (!entry.path.startsWith('.build/') && entry.path !== '.build') continue
    const digest = createHash('sha256')
    const expected = children.get(entry.path) ?? []
    expected.sort((left, right) => Buffer.compare(Buffer.from(left.name), Buffer.from(right.name)))
    for (const child of expected) {
      digest.update(child.type)
      digest.update(Buffer.from([0]))
      digest.update(child.name)
      digest.update(Buffer.from([0]))
    }
    const line = `${JSON.stringify({
      path: entry.path, mtimeNs: entry.mtimeNs, mode: entry.mode,
      children: expected.length, childrenSha256: digest.digest('hex'),
    })}\n`
    inputBytes += Buffer.byteLength(line)
    if (inputBytes > 16 * 1024 * 1024 || Date.now() > deadline) {
      throw new Error('Android state directory timestamps exceeded byte or time cap')
    }
    lines.push(line)
  }
  const remaining = deadline - Date.now()
  if (remaining <= 0) throw new Error('Android state directory timestamp restoration timed out')
  const result = spawnSync('python3', ['-I', fileURLToPath(new URL('./android-state-dir-ns.py', import.meta.url))], {
    cwd: staging,
    input: lines.join(''), encoding: 'utf8', timeout: remaining, maxBuffer: 8192,
  })
  if (result.error || result.status !== 0) {
    throw new Error(`Android state exact directory timestamps could not be restored: ${result.error?.message ?? result.stderr.trim()}`)
  }
}

export async function packState({ packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity, limits = defaultLimits }) {
  assertIdentity(identity)
  mkdirSync(artifactRoot, { recursive: true })
  const inventory = inventoryState(packageRoot, cacheRoot, checkoutRoot, limits)
  const sourceMetadata = await inventoryTrackedSourceMetadata({ checkoutRoot, identity })
  const sourceMetadataBytes = Buffer.from(JSON.stringify(sourceMetadata))
  if (sourceMetadataBytes.length > sourceMetadataLimits.metadataBytes ||
      inventory.entries.length + 1 > limits.entries ||
      inventory.unpackedBytes + sourceMetadataBytes.length > limits.unpackedBytes) {
    throw new Error('Android source metadata exceeded archive bounds')
  }
  const archive = join(artifactRoot, archiveName)
  const started = Date.now()
  const deadline = started + limits.transferMs
  const digest = createHash('sha256')
  let compressedBytes = 0
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), limits.transferMs)
  try {
    await pipeline(
      Readable.from(archiveChunks(inventory.entries, identity, deadline, sourceMetadataBytes)),
      createGzip({ level: 1 }),
      new Transform({
        transform(chunk, _encoding, callback) {
          compressedBytes += chunk.length
          if (compressedBytes > limits.compressedBytes || Date.now() > deadline) {
            callback(new Error('Android state exceeded compressed-byte or compression-time cap'))
          } else {
            digest.update(chunk)
            callback(null, chunk)
          }
        },
      }),
      createWriteStream(archive, { flags: 'wx', mode: 0o600 }),
      { signal: controller.signal },
    )
  } catch (error) {
    rmSync(archive, { force: true })
    throw error
  } finally {
    clearTimeout(timer)
  }
  const manifest = {
    format: 4,
    identity,
    sha256: digest.digest('hex'),
    entries: inventory.entries.length + 1,
    unpackedBytes: inventory.unpackedBytes + sourceMetadataBytes.length,
    compressedBytes,
    sourceMetadataBytes: sourceMetadataBytes.length,
  }
  writeFileSync(join(artifactRoot, manifestName), `${JSON.stringify(manifest)}\n`, { flag: 'wx', mode: 0o600 })
  return { ...manifest, scanMs: inventory.scanMs, packMs: Date.now() - started }
}

function checkedDestination(root, path) {
  assertMemberPath(path)
  const target = join(root, path)
  if (!target.startsWith(`${root}${sep}`)) throw new Error(`Android state path escaped root: ${path}`)
  return target
}

export async function restoreState({ packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity, limits = defaultLimits }) {
  assertIdentity(identity)
  assertPackageLayout(packageRoot, checkoutRoot)
  const manifestPath = join(artifactRoot, manifestName)
  if (statSync(manifestPath).size > 8192) throw new Error('Android state manifest exceeds 8 KiB')
  const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'))
  if (manifest.format !== 4 || JSON.stringify(manifest.identity) !== JSON.stringify(identity) ||
      !Number.isSafeInteger(manifest.entries) || manifest.entries > limits.entries ||
      !Number.isSafeInteger(manifest.unpackedBytes) || manifest.unpackedBytes > limits.unpackedBytes ||
      !Number.isSafeInteger(manifest.compressedBytes) || manifest.compressedBytes > limits.compressedBytes ||
      !Number.isSafeInteger(manifest.sourceMetadataBytes) || manifest.sourceMetadataBytes <= 0 ||
      manifest.sourceMetadataBytes > sourceMetadataLimits.metadataBytes ||
      !/^[0-9a-f]{64}$/u.test(manifest.sha256)) {
    throw new Error('Android state manifest identity or limits mismatch')
  }
  const archive = join(artifactRoot, archiveName)
  if (statSync(archive).size !== manifest.compressedBytes) throw new Error('Android state archive size mismatch')
  const started = Date.now()
  const deadline = started + limits.transferMs
  const hash = await sha256File(archive, limits.compressedBytes, deadline)
  if (hash.digest !== manifest.sha256) throw new Error('Android state archive hash mismatch')
  const staging = join(artifactRoot, 'extracted')
  if (existsSync(staging)) throw new Error('Android state extraction target already exists')
  mkdirSync(staging, { mode: 0o700 })
  mkdirSync(join(staging, 'Android'), { mode: 0o700 })
  const reader = new ByteReader(createReadStream(archive).pipe(createGunzip()), deadline, limits.unpackedBytes + 2 * 8192 * limits.entries)
  const observed = { entries: 0, unpackedBytes: 0 }
  const fileTimes = []
  const directoryModes = []
  const expectedChildren = new Map()
  const deferredLinks = []
  let sourceMetadata = null
  try {
    if (!(await reader.read(magic.length)).equals(magic)) throw new Error('Android state magic mismatch')
    const archiveIdentity = await reader.header()
    if (JSON.stringify(archiveIdentity) !== JSON.stringify(identity)) throw new Error('Android state header identity mismatch')
    let previous = ''
    let member
    while ((member = await reader.header()) !== null) {
      observed.entries += 1
      if (observed.entries > limits.entries || member.path <= previous) {
        throw new Error('Android state member count or order invalid')
      }
      previous = member.path
      if (member.path === sourceMetadataPath) {
        if (sourceMetadata !== null || member.type !== 'source-metadata' ||
            member.size !== manifest.sourceMetadataBytes) {
          throw new Error('Android source metadata member invalid')
        }
        const chunks = []
        let remaining = member.size
        while (remaining) {
          const chunk = await reader.read(Math.min(remaining, 64 * 1024))
          chunks.push(chunk)
          remaining -= chunk.length
        }
        sourceMetadata = JSON.parse(Buffer.concat(chunks, member.size).toString('utf8'))
        observed.unpackedBytes += member.size
        continue
      }
      if (sourceMetadata !== null) throw new Error('Android state member follows source metadata')
      const target = checkedDestination(staging, member.path)
      const parentPath = member.path.slice(0, member.path.lastIndexOf('/'))
      if (parentPath === '.build' || parentPath.startsWith('.build/')) {
        const siblings = expectedChildren.get(parentPath) ?? []
        siblings.push({ name: member.path.slice(parentPath.length + 1), type: member.type })
        expectedChildren.set(parentPath, siblings)
      }
      const parent = dirname(target)
      if (!lstatSync(parent).isDirectory()) throw new Error(`Android state parent is not a directory: ${member.path}`)
      if (!Number.isSafeInteger(member.mode) || member.mode < 0 || member.mode > 0o777) {
        throw new Error(`Android state mode is invalid: ${member.path}`)
      }
      if (member.type === 'directory' && member.size === 0 && validTimestamp(member.mtimeNs)) {
        mkdirSync(target, { mode: 0o700 })
        directoryModes.push({ target, path: member.path, mode: member.mode, mtimeNs: member.mtimeNs })
      } else if (member.type === 'symlink' && member.size === 0 && member.mtimeNs === null) {
        if (member.path !== '.build/debug') {
          deferredLinks.push({ target, link: member.target, path: member.path, generated: true })
        } else {
          assertDebugLink(member.path, member.target)
          deferredLinks.push({ target, link: member.target, path: member.path, generated: false })
        }
      } else if (member.type === 'file' && Number.isSafeInteger(member.size) && member.size >= 0 &&
          validTimestamp(member.mtimeNs)) {
        observed.unpackedBytes += member.size
        if (observed.unpackedBytes > limits.unpackedBytes) throw new Error('Android state file-byte cap exceeded')
        const file = createWriteStream(target, { flags: 'wx', mode: 0o600 })
        try {
          let remaining = member.size
          for await (const chunk of (async function* () {
            while (remaining) {
              const data = await reader.read(Math.min(remaining, 64 * 1024))
              remaining -= data.length
              yield data
            }
          })()) {
            if (!file.write(chunk)) await new Promise(resolveDrain => file.once('drain', resolveDrain))
          }
          await new Promise((resolveFinish, reject) => file.end(error => error ? reject(error) : resolveFinish()))
          chmodSync(target, member.mode)
          fileTimes.push({ path: member.path, mtimeNs: member.mtimeNs })
        } catch (error) {
          file.destroy()
          throw error
        }
      } else {
        throw new Error(`Android state member type invalid: ${member.path}`)
      }
    }
    await reader.end()
    if (sourceMetadata === null || observed.entries !== manifest.entries ||
        observed.unpackedBytes !== manifest.unpackedBytes) {
      throw new Error('Android state unpacked inventory mismatch')
    }
    restoreExactFileTimes(staging, fileTimes, deadline)
    for (const { target, link, path, generated } of deferredLinks) {
      if (generated) {
        checkedSkipstoneLink(path, link, packageRoot, checkoutRoot)
      } else if (!lstatSync(join(dirname(target), link)).isDirectory()) {
        throw new Error(`Android state debug symlink target is missing: ${target}`)
      }
      symlinkSync(link, target)
    }
    for (const { path, link, generated } of deferredLinks) {
      if (generated) resolveSkipstoneLink(path, link, packageRoot, checkoutRoot, staging, deadline)
    }
    for (const { target, mode } of directoryModes.reverse()) chmodSync(target, mode)
    restoreExactDirectoryTimes(staging, directoryModes, expectedChildren, deadline)
    for (const directory of [packageRoot, join(packageRoot, 'Android'), dirname(cacheRoot)]) {
      const stat = lstatSync(directory)
      if (stat.isSymbolicLink() || !stat.isDirectory()) {
        throw new Error(`Android state destination parent is unsafe: ${directory}`)
      }
    }
    const destinations = selectedRoots(packageRoot, cacheRoot)
    for (const { path, source } of destinations) {
      const staged = checkedDestination(staging, path)
      if (!lstatSync(staged).isDirectory()) throw new Error(`Android state root missing: ${path}`)
      const stat = existingStat(source)
      if (stat && (stat.isSymbolicLink() || !stat.isDirectory())) {
        throw new Error(`Android state destination is unsafe: ${path}`)
      }
    }
    // A failed source identity or byte check leaves every tracked source timestamp untouched.
    await restoreTrackedSourceTimes({ checkoutRoot, identity, metadata: sourceMetadata })
    for (const { path, source } of destinations) {
      const staged = checkedDestination(staging, path)
      if (existsSync(source)) rmSync(source, { recursive: true })
      mkdirSync(dirname(source), { recursive: true })
      renameSync(staged, source)
    }
    return { ...manifest, restoreMs: Date.now() - started }
  } finally {
    rmSync(staging, { recursive: true, force: true })
  }
}
