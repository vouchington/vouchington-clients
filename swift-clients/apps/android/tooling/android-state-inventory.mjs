import { execFileSync } from 'node:child_process'
import { lstatSync, opendirSync, readlinkSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'

export const defaultLimits = {
  entries: 150_000,
  unpackedBytes: 8 * 1024 ** 3,
  compressedBytes: 5 * 1024 ** 3,
  scanMs: 90_000,
  transferMs: 180_000,
}
const admittedRoots = ['.build', 'Android/.gradle', 'gradle-build-cache']

export function assertIdentity(identity) {
  if (!/^[0-9a-f]{40}$/u.test(identity.revision) ||
      !/^[1-9][0-9]*$/u.test(identity.run) ||
      !/^[1-9][0-9]*$/u.test(identity.attempt) ||
      !/^\d+\.\d+\.\d+$/u.test(identity.gradle)) {
    throw new Error('Invalid exact-run Android state identity')
  }
}

export function assertCandidate(checkoutRoot, identity) {
  const revision = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: checkoutRoot, encoding: 'utf8' }).trim()
  if (revision !== identity.revision) throw new Error('Android state candidate checkout identity mismatch')
}

export function assertPackageLayout(packageRoot, checkoutRoot) {
  const parts = [checkoutRoot, join(checkoutRoot, 'swift-clients'), join(checkoutRoot, 'swift-clients/apps'), join(checkoutRoot, 'swift-clients/apps/android')]
  if (parts.at(-1) !== resolve(packageRoot)) throw new Error('Unexpected Android package layout')
  for (const path of parts) {
    const stat = lstatSync(path)
    if (stat.isSymbolicLink() || !stat.isDirectory()) {
      throw new Error(`Android state package ancestor is unsafe: ${path}`)
    }
  }
}

export function assertMemberPath(path) {
  if (typeof path !== 'string' || path.length > 4096 || path.includes('\\') ||
      path.startsWith('/') || Array.from(path).some(character => {
        const code = character.codePointAt(0)
        return code < 32 || code === 127
      })) {
    throw new Error(`Unsafe Android state member: ${path}`)
  }
  const parts = path.split('/')
  if (parts.some(part => part === '' || part === '.' || part === '..')) {
    throw new Error(`Unsafe Android state member: ${path}`)
  }
  if (!admittedRoots.some(root => path === root || path.startsWith(`${root}/`))) {
    throw new Error(`Unexpected Android state member: ${path}`)
  }
}

export function assertDebugLink(path, target) {
  const nestedSwiftScratch = /^\.build\/plugins\/outputs\/[^/]+\/[^/]+\/destination\/skipstone\/[^/]+\/build\/swift\/debug$/u.test(path)
  if ((path !== '.build/debug' && !nestedSwiftScratch) || typeof target !== 'string' ||
      !/^[A-Za-z0-9._-]+\/debug$/u.test(target) || target.split('/').includes('..')) {
    throw new Error(`Unsafe Android state symlink: ${path}`)
  }
}

export function selectedRoots(packageRoot, cacheRoot) {
  return [
    { path: '.build', source: join(packageRoot, '.build') },
    { path: 'Android/.gradle', source: join(packageRoot, 'Android/.gradle') },
    { path: 'gradle-build-cache', source: cacheRoot },
  ]
}

export function inventoryState(packageRoot, cacheRoot, checkoutRoot, limits = defaultLimits) {
  assertPackageLayout(packageRoot, checkoutRoot)
  const started = Date.now()
  const pending = selectedRoots(packageRoot, cacheRoot)
  const entries = []
  let unpackedBytes = 0
  while (pending.length) {
    if (Date.now() - started > limits.scanMs || entries.length >= limits.entries) {
      throw new Error('Android state inventory exceeded its time or member cap')
    }
    const item = pending.pop()
    assertMemberPath(item.path)
    const stat = lstatSync(item.source)
    if (stat.isSymbolicLink()) {
      const target = readlinkSync(item.source)
      assertDebugLink(item.path, target)
      if (!lstatSync(join(dirname(item.source), target)).isDirectory()) {
        throw new Error(`Android state debug symlink target is not a directory: ${item.path}`)
      }
      entries.push({ path: item.path, type: 'symlink', target, mode: 0 })
      continue
    }
    if ((stat.mode & 0o7000) !== 0) throw new Error(`Android state contains special permissions: ${item.path}`)
    if (stat.isDirectory()) {
      entries.push({ path: item.path, type: 'directory', source: item.source, mode: stat.mode & 0o777 })
      const directory = opendirSync(item.source)
      try {
        let child
        while ((child = directory.readSync()) !== null) {
          if (entries.length + pending.length >= limits.entries || Date.now() - started > limits.scanMs) {
            throw new Error('Android state inventory exceeded its time or member cap')
          }
          pending.push({ path: `${item.path}/${child.name}`, source: join(item.source, child.name) })
        }
      } finally {
        directory.closeSync()
      }
    } else if (stat.isFile()) {
      unpackedBytes += stat.size
      if (unpackedBytes > limits.unpackedBytes) {
        throw new Error('Android state inventory exceeded its unpacked-byte cap')
      }
      entries.push({
        path: item.path,
        type: 'file',
        size: stat.size,
        source: item.source,
        mode: stat.mode & 0o777,
        mtimeMs: stat.mtimeMs,
      })
    } else {
      throw new Error(`Android state contains a non-file member: ${item.path}`)
    }
  }
  entries.sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0))
  return { entries, unpackedBytes, scanMs: Date.now() - started }
}
