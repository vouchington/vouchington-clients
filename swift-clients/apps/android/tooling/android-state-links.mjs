import { lstatSync, readlinkSync } from 'node:fs'
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path'
import { isTestRunnerRole, testRunnerDebugTarget } from './android-state-role.mjs'

export function targetDiagnostic(target) {
  const text = JSON.stringify(target) ?? String(target)
  const bytes = Buffer.from(text)
  if (bytes.length <= 1024) return text
  let end = 1021
  while ((bytes[end] & 0xc0) === 0x80) end--
  return `${bytes.subarray(0, end).toString('utf8')}...`
}

export function assertDebugLink(path, target, role = 'native') {
  const validTarget = isTestRunnerRole(role)
    ? target === testRunnerDebugTarget
    : typeof target === 'string' && /^[A-Za-z0-9._-]+\/debug$/u.test(target) && !target.split('/').includes('..')
  if (path !== '.build/debug' || !validTarget) {
    throw new Error(`Unsafe Android state symlink: ${path}; target=${targetDiagnostic(target)}`)
  }
}

const skipstoneOutputPath = /^\.build\/plugins\/outputs\/[^/]+\/[^/]+\/destination\/skipstone\/[^/]+(?:\/[^/]+)*$/u

export function isSkipstoneOutputPath(path) {
  return skipstoneOutputPath.test(path)
}

// Skipstone 1.9.13 creates absolute source/resource links and relative module,
// resource-alias and test links within its generated output. Preserve their
// checkout-relative relationship, never the producer runner's absolute path.
export function checkedSkipstoneLink(path, target, packageRoot, checkoutRoot) {
  const targetParts = typeof target === 'string' ? target.split(sep) : []
  const firstDescendant = targetParts.findIndex(part => part !== '..')
  if (!skipstoneOutputPath.test(path) || typeof target !== 'string' || target.length > 4096 ||
      !target || isAbsolute(target) || target.includes('\\') || target.includes('\0') ||
      firstDescendant < 0 || targetParts.some((part, index) =>
        part === '' || part === '.' || (part === '..' && index >= firstDescendant))) {
    throw new Error(`Unsafe Android state symlink: ${path}`)
  }
  const finalLink = join(packageRoot, path)
  const finalTarget = resolve(dirname(finalLink), target)
  const checkoutRelative = relative(checkoutRoot, finalTarget)
  if (!checkoutRelative.startsWith(`swift-clients${sep}`) ||
      checkoutRelative.split(sep).includes('..')) {
    throw new Error(`Android state mirror target escaped candidate checkout: ${path}`)
  }
  return target
}

// Resolve each component before processing `..`: a symlink/../ path follows the
// symlink on disk, whereas path.resolve would silently erase that component.
export function resolveSkipstoneLink(path, target, packageRoot, checkoutRoot, stagedRoot = null, deadline = Infinity) {
  checkedSkipstoneLink(path, target, packageRoot, checkoutRoot)
  const swiftRoot = join(checkoutRoot, 'swift-clients')
  const selectedBuild = join(packageRoot, '.build')
  const stateRoot = stagedRoot ?? packageRoot
  const active = new Set()
  let steps = 0
  let links = 0
  const contained = (root, value) => value === root || value.startsWith(`${root}${sep}`)
  const actual = virtual => contained(selectedBuild, virtual)
    ? join(stateRoot, relative(packageRoot, virtual))
    : virtual
  const check = virtual => {
    if (!contained(swiftRoot, virtual) ||
        (!contained(selectedBuild, virtual) && relative(swiftRoot, virtual).split(sep).includes('.build'))) {
      throw new Error(`Android state link graph escaped selected candidate state: ${path}`)
    }
  }
  const walk = (start, components) => {
    let current = start
    check(current)
    for (const part of components) {
      if (Date.now() > deadline) throw new Error(`Android state link graph exceeded time cap: ${path}`)
      if (++steps > 512) throw new Error(`Android state link graph exceeded component cap: ${path}`)
      if (part === '' || part === '.') continue
      if (part === '..') {
        if (!lstatSync(actual(current)).isDirectory()) {
          throw new Error(`Android state link graph has a non-directory parent: ${path}`)
        }
        current = dirname(current)
        check(current)
        continue
      }
      if (part.includes('\\') || part.includes('\0')) {
        throw new Error(`Android state link graph has an invalid component: ${path}`)
      }
      const next = join(current, part)
      check(next)
      const stat = lstatSync(actual(next))
      if (stat.isSymbolicLink()) {
        if (++links > 32 || active.has(next)) {
          throw new Error(`Android state link graph has a cycle or exceeds depth cap: ${path}`)
        }
        active.add(next)
        const link = readlinkSync(actual(next))
        if (link.length > 4096 || link.includes('\\') || link.includes('\0')) {
          throw new Error(`Android state link graph has an invalid or oversized hop: ${path}`)
        }
        if (isAbsolute(link)) {
          if (!link.startsWith(`${swiftRoot}${sep}`)) {
            throw new Error(`Android state link graph escaped selected candidate state: ${path}`)
          }
          current = walk(swiftRoot, link.slice(swiftRoot.length + 1).split(sep))
        } else {
          current = walk(dirname(next), link.split(sep))
        }
        active.delete(next)
      } else if (stat.isDirectory() || stat.isFile()) {
        current = next
      } else {
        throw new Error(`Android state link graph has a non-file target: ${path}`)
      }
    }
    return current
  }
  const destination = walk(dirname(join(packageRoot, path)), target.split(sep))
  const terminal = lstatSync(actual(destination))
  if (!terminal.isFile() && !terminal.isDirectory()) {
    throw new Error(`Android state link graph has no file or directory target: ${path}`)
  }
  return destination
}
