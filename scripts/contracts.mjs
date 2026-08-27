import { cp, lstat, readdir, readFile, rm } from 'node:fs/promises'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repositoryRoot = resolve(import.meta.dirname, '..')
const declaredPaths = [
  { source: 'api-fixtures/v1', destination: 'api-fixtures/v1' },
  { source: 'client-contracts/v1/native-localization', destination: 'native-localization' },
]
const localizationTargets = [
  {
    source: 'client-contracts/v1/native-localization/swift',
    destination: resolve(repositoryRoot, 'swift-clients/ui/Sources/VouchaLocalization/Generated'),
  },
  {
    source: 'client-contracts/v1/native-localization/dotnet',
    destination: resolve(
      repositoryRoot,
      'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
    ),
  },
]

function fail(message) {
  throw new Error(`Contract check failed: ${message}`)
}

async function directory(path, label) {
  let info
  try {
    info = await lstat(path)
  } catch (error) {
    fail(`cannot read ${label} at ${path}: ${error.message}`)
  }
  if (info.isSymbolicLink() || !info.isDirectory())
    fail(`${label} is not a real directory: ${path}`)
}

async function filesUnder(root, label) {
  await directory(root, label)
  const files = []
  async function walk(path) {
    for (const entry of await readdir(path, { withFileTypes: true })) {
      const child = join(path, entry.name)
      if (entry.isSymbolicLink()) fail(`symlink found at ${relative(root, child)}`)
      if (entry.isDirectory()) await walk(child)
      else if (entry.isFile()) files.push(relative(root, child))
      else fail(`unsupported filesystem entry at ${relative(root, child)}`)
    }
  }
  await walk(root)
  return files.sort()
}

async function contractRoot({
  root,
  config = resolve(repositoryRoot, 'contracts/filaments.json'),
} = {}) {
  const configuredRoot = root ?? process.env.VOUCHA_FILAMENTS_CONTRACT_ROOT
  if (typeof configuredRoot !== 'string' || configuredRoot.trim() === '')
    fail('VOUCHA_FILAMENTS_CONTRACT_ROOT is required')
  const checkoutRoot = resolve(configuredRoot)
  await directory(checkoutRoot, 'Filaments contract root')
  let parsed
  try {
    parsed = JSON.parse(await readFile(config, 'utf8'))
  } catch (error) {
    fail(`cannot read contract configuration at ${config}: ${error.message}`)
  }
  if (
    parsed.schemaVersion !== 1 ||
    parsed.repository !== 'jonathanong/filaments' ||
    parsed.ref !== 'main' ||
    JSON.stringify(parsed.paths) !== JSON.stringify(declaredPaths)
  )
    fail('contract configuration does not match the client contract')
  for (const path of declaredPaths)
    await filesUnder(
      join(checkoutRoot, path.source),
      `declared Filaments contract source ${path.source}`,
    )
  return checkoutRoot
}

async function sameTree(source, destination) {
  const [sourceFiles, destinationFiles] = await Promise.all([
    filesUnder(source, 'declared Filaments localization source'),
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
  for (const target of options.targets ?? localizationTargets) {
    if (!(await sameTree(join(root, target.source), target.destination))) {
      fail(`generated localization differs at ${target.destination}; run pnpm run contracts:sync`)
    }
  }
}

export async function syncContracts(options = {}) {
  const root = await contractRoot(options)
  for (const target of options.targets ?? localizationTargets) {
    await directory(target.destination, 'generated localization target')
    await rm(target.destination, { recursive: true })
    await cp(join(root, target.source), target.destination, { recursive: true, dereference: false })
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const command = process.argv[2]
  if (command === 'check') await checkContracts()
  else if (command === 'sync') await syncContracts()
  else throw new Error('Usage: node scripts/contracts.mjs <check|sync>')
}
