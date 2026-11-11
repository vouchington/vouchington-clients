import { cp, lstat, mkdtemp, readdir, readFile, rename, rm } from 'node:fs/promises'
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { prefixedFailure } from './prefixed-failure.mjs'
import {
  contractConfigPath,
  contractDataPaths,
  declaredContractPaths,
  localizationPaths,
  readContractConfig,
} from './contract-config.mjs'
export { contractDataPaths, declaredContractPaths, localizationPaths }
const repositoryRoot = resolve(import.meta.dirname, '..')
function localizationTargets(destinationRoot = repositoryRoot) {
  return localizationPaths.map(source => ({
    source,
    destination: resolve(destinationRoot, source),
  }))
}
const fail = prefixedFailure('Contract check failed')
async function pathInfo(path) {
  try {
    return await lstat(path)
  } catch (error) {
    if (error.code === 'ENOENT') return undefined
    throw error
  }
}
async function directory(path, label) {
  let info
  try {
    info = await lstat(path)
  } catch (error) {
    fail(`cannot read ${label} at ${path}: ${error.message}`)
  }
  if (info.isSymbolicLink()) fail(`${label} must not be a symbolic link: ${path}`)
  if (!info.isDirectory()) fail(`${label} is not a real directory: ${path}`)
}
async function descendantDirectory(root, path, label) {
  await directory(root, 'client checkout root')
  const child = relative(root, path)
  if (child === '' || child === '..' || child.startsWith(`..${sep}`) || isAbsolute(child))
    fail(`${label} must be inside the client checkout root: ${path}`)
  let current = root
  for (const component of child.split(sep).filter(Boolean)) {
    current = join(current, component)
    await directory(current, label)
  }
}
async function descendantParentDirectory(root, path, label) {
  const parent = dirname(path)
  if (parent === root) await directory(root, 'client checkout root')
  else await descendantDirectory(root, parent, label)
}
function candidateRoot(value) {
  if (value === undefined) return repositoryRoot
  if (
    typeof value !== 'string' ||
    value.includes('\0') ||
    value.trim() === '' ||
    !isAbsolute(value.trim())
  )
    fail('destination root must be an absolute client checkout path')
  const resolved = resolve(value.trim())
  if (resolved === resolve(sep)) fail('destination root must not be the filesystem root')
  return resolved
}
async function filesUnder(root, label) {
  await directory(root, label)
  const files = []
  async function walk(path) {
    for (const entry of await readdir(path, { withFileTypes: true })) {
      const child = join(path, entry.name)
      if (entry.isSymbolicLink()) fail(`symlink found at ${relative(root, child) || '.'}`)
      if (entry.isDirectory()) await walk(child)
      else if (entry.isFile()) files.push(relative(root, child))
      else fail(`unsupported filesystem entry at ${relative(root, child) || '.'}`)
    }
  }
  await walk(root)
  return files.sort()
}
async function contractRoot({ root, config = contractConfigPath } = {}) {
  const configuredRoot = root ?? process.env.VOUCHA_CONTRACT_ROOT
  if (typeof configuredRoot !== 'string' || configuredRoot.trim() === '')
    fail('VOUCHA_CONTRACT_ROOT is required; export the root printed by pnpm run contracts:pull')
  const checkoutRoot = resolve(configuredRoot.trim())
  await directory(checkoutRoot, 'Vouchington contract root')
  await readContractConfig(config)
  for (const path of declaredContractPaths)
    await filesUnder(join(checkoutRoot, path), `declared Vouchington contract source ${path}`)
  return checkoutRoot
}
async function sameTree(source, destination) {
  const [sourceFiles, destinationFiles] = await Promise.all([
    filesUnder(source, 'declared Vouchington localization source'),
    filesUnder(destination, 'generated localization target'),
  ])
  if (JSON.stringify(sourceFiles) !== JSON.stringify(destinationFiles)) return false
  for (const file of sourceFiles) {
    if (
      !Buffer.from(await readFile(join(source, file))).equals(
        await readFile(join(destination, file)),
      )
    )
      return false
  }
  return true
}
export async function verifyContract(options = {}) {
  return contractRoot(options)
}
export async function checkContracts(options = {}) {
  const root = await contractRoot(options)
  const destinationRoot = candidateRoot(options.destinationRoot)
  const targets = options.targets ?? localizationTargets(destinationRoot)
  for (const target of targets) {
    await descendantParentDirectory(
      destinationRoot,
      target.destination,
      'generated localization target parent',
    )
    if (!(await pathInfo(target.destination)))
      fail(
        `generated localization is missing at ${target.destination}; run pnpm run contracts:pull`,
      )
    await descendantDirectory(destinationRoot, target.destination, 'generated localization target')
    if (!(await sameTree(join(root, target.source), target.destination))) {
      fail(`generated localization differs at ${target.destination}; run pnpm run contracts:pull`)
    }
  }
}
export async function syncContracts(options = {}) {
  const root = await contractRoot(options)
  if (options.destinationRoot !== undefined) fail('sync destination root is not configurable')
  for (const target of options.targets ?? localizationTargets()) {
    await descendantParentDirectory(
      repositoryRoot,
      target.destination,
      'generated localization target parent',
    )
    const backup = `${target.destination}.contracts-backup`
    const backupInfo = await pathInfo(backup)
    if (backupInfo) {
      if (backupInfo.isSymbolicLink() || !backupInfo.isDirectory())
        fail(`generated localization backup is not a real directory: ${backup}`)
      const destinationInfo = await pathInfo(target.destination)
      if (destinationInfo) {
        await directory(target.destination, 'generated localization target')
        await rm(backup, { recursive: true })
      } else await rename(backup, target.destination)
    }
    const destinationInfo = await pathInfo(target.destination)
    if (destinationInfo) await directory(target.destination, 'generated localization target')
    const stagingRoot = await mkdtemp(join(dirname(target.destination), '.contracts-stage-'))
    const staged = join(stagingRoot, 'generated')
    try {
      await cp(join(root, target.source), staged, { recursive: true, dereference: false })
      if (destinationInfo) {
        await rename(target.destination, backup)
        try {
          await rename(staged, target.destination)
        } catch (error) {
          await rename(backup, target.destination)
          throw error
        }
        await rm(backup, { recursive: true })
      } else await rename(staged, target.destination)
    } finally {
      await rm(stagingRoot, { recursive: true, force: true })
    }
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const [command, ...extra] = process.argv.slice(2)
  const usage =
    'Usage: node scripts/contracts.mjs check [--destination-root <absolute-client-checkout>]\nUsage: node scripts/contracts.mjs verify\nUsage: node scripts/contracts.mjs sync (low-level; use contracts:pull)'
  const validCheck =
    command === 'check' &&
    extra.length === 2 &&
    extra[0] === '--destination-root' &&
    extra[1]?.trim()
  if (extra.length !== 0 && !validCheck) throw new Error(usage)
  const options = extra.length === 2 ? { destinationRoot: extra[1] } : {}
  if (command === 'check') await checkContracts(options)
  else if (command === 'verify' && extra.length === 0) await verifyContract()
  else if (command === 'sync' && extra.length === 0) await syncContracts()
  else throw new Error(usage)
}
