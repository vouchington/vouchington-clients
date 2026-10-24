import { linkSync, lstatSync, mkdirSync, mkdtempSync, rmSync, unlinkSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve, sep } from 'node:path'
import { assertCandidate, assertIdentity, assertPackageLayout, existingStat } from './android-state-inventory.mjs'
import { cacheLimits, checkTime, entryName, fileDigest, inventory, safeDirectory } from './android-kotlin-cache-files.mjs'

const role = 'kotlin-libraries'
const manifestName = 'manifest.json'

function context(options) {
  const { packageRoot, checkoutRoot, cacheRoot, artifactRoot, identity, configurationSha256 } = options
  assertIdentity(identity)
  assertCandidate(checkoutRoot, identity)
  assertPackageLayout(packageRoot, checkoutRoot)
  if (!/^[a-f0-9]{64}$/u.test(configurationSha256)) throw Error('Invalid Kotlin cache configuration identity')
  safeDirectory(cacheRoot)
  safeDirectory(dirname(artifactRoot))
  if (artifactRoot !== resolve(artifactRoot) || cacheRoot === artifactRoot ||
      artifactRoot.startsWith(cacheRoot + sep) || cacheRoot.startsWith(artifactRoot + sep)) throw Error('Overlapping Kotlin cache roots')
  const limits = { ...cacheLimits, ...options.limits }
  return { limits, deadline: Date.now() + limits.ms }
}

function manifestEntries(manifest, identity, configurationSha256, limits) {
  if (manifest?.format !== 1 || manifest.role !== role ||
      Object.keys(identity).some(key => manifest.identity?.[key] !== identity[key]) ||
      manifest.configurationSha256 !== configurationSha256) throw Error('Kotlin cache artifact identity mismatch')
  if (!Array.isArray(manifest.entries) || !manifest.entries.length || manifest.entries.length > limits.entries) throw Error('Invalid Kotlin cache manifest entries')
  let total = 0
  const names = new Set()
  for (const entry of manifest.entries) {
    if (!entry || typeof entry !== 'object' || Array.isArray(entry) || !entryName.test(entry.name) || names.has(entry.name) || !Number.isSafeInteger(entry.size) || entry.size < 0 ||
        !/^[a-f0-9]{64}$/u.test(entry.sha256)) throw Error('Invalid Kotlin cache manifest member')
    names.add(entry.name)
    total += entry.size
    if (total > limits.bytes) throw Error('Kotlin cache byte limit exceeded')
  }
  return manifest.entries
}

function same(left, right) {
  return left.size === right.size && left.sha256 === right.sha256
}

export function packKotlinCache(options) {
  const { cacheRoot, artifactRoot, identity, configurationSha256 } = options
  const { limits, deadline } = context(options)
  const entries = inventory(cacheRoot, limits, deadline)
  if (!entries.length) throw Error('Kotlin cache has no task entries')
  const manifest = { format: 1, role, identity, configurationSha256, entries }
  const serialized = JSON.stringify(manifest) + '\n'
  if (Buffer.byteLength(serialized) > limits.manifestBytes) throw Error('Kotlin cache manifest limit exceeded')
  mkdirSync(artifactRoot, { mode: 0o700 })
  try {
    for (const entry of entries) {
      const copied = fileDigest(join(cacheRoot, entry.name), entry.size, deadline, join(artifactRoot, entry.name))
      if (!same(copied, entry)) throw Error('Kotlin cache changed during pack')
    }
    checkTime(deadline)
    writeFileSync(join(artifactRoot, manifestName), serialized, { flag: 'wx', mode: 0o600 })
    return { role, entries: entries.length, bytes: entries.reduce((n, e) => n + e.size, 0) }
  } catch (error) { rmSync(artifactRoot, { recursive: true }); throw error }
}

export function mergeKotlinCache(options) {
  const { cacheRoot, artifactRoot, identity, configurationSha256, linkEntry = linkSync } = options
  const { limits, deadline } = context(options)
  safeDirectory(artifactRoot)
  const manifestPath = join(artifactRoot, manifestName)
  const manifestStat = lstatSync(manifestPath)
  if (!manifestStat.isFile() || manifestStat.isSymbolicLink() || manifestStat.size > limits.manifestBytes) throw Error('Unsafe Kotlin cache manifest')
  const manifest = JSON.parse(fileDigest(manifestPath, limits.manifestBytes, deadline, undefined, true).text)
  const expected = manifestEntries(manifest, identity, configurationSha256, limits)
  const incoming = new Map(inventory(artifactRoot, limits, deadline, new Set([manifestName])).map(entry => [entry.name, entry]))
  if (incoming.size !== expected.length || expected.some(entry => !same(entry, incoming.get(entry.name) ?? {}))) throw Error('Kotlin cache artifact inventory mismatch')
  const existing = new Map(inventory(cacheRoot, limits, deadline).map(entry => [entry.name, entry]))
  const additions = []
  for (const entry of expected) {
    if (existing.has(entry.name)) {
      if (!same(entry, existing.get(entry.name))) throw Error('Conflicting existing Kotlin cache entry')
    } else additions.push(entry)
  }
  const combined = [...existing.values(), ...additions]
  if (combined.length > limits.entries || combined.reduce((n, e) => n + e.size, 0) > limits.bytes) throw Error('Merged Kotlin cache exceeds bounds')
  // Stage outside the destination root on its filesystem, before publishing anything.
  const staging = mkdtempSync(join(dirname(cacheRoot), '.kotlin-cache-'))
  const published = []
  try {
    for (const entry of additions) {
      if (!same(fileDigest(join(artifactRoot, entry.name), entry.size, deadline, join(staging, entry.name)), entry)) throw Error('Kotlin cache artifact changed during stage')
    }
    for (const entry of additions) {
      checkTime(deadline)
      const staged = join(staging, entry.name), destination = join(cacheRoot, entry.name)
      const stat = lstatSync(staged)
      linkEntry(staged, destination) // Atomic no-overwrite publication; EEXIST is fatal.
      published.push({ destination, dev: stat.dev, ino: stat.ino })
    }
    return { role, added: additions.length, retained: expected.length - additions.length }
  } catch (error) {
    const failures = []
    for (const own of published.reverse()) {
      try {
        const stat = existingStat(own.destination)
        if (stat?.dev === own.dev && stat.ino === own.ino) unlinkSync(own.destination)
      } catch (failure) { failures.push(failure) }
    }
    if (failures.length) throw new AggregateError([error, ...failures], 'Kotlin cache publication failed; rollback incomplete')
    throw error
  } finally { rmSync(staging, { recursive: true }) }
}
