import { cp, lstat, mkdir, readdir, readFile } from 'node:fs/promises'
import { isAbsolute, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
import { declaredFilamentsContractPaths } from './contracts.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const configurationPath = join(repositoryRoot, 'contracts/filaments.json')
const filamentsExporterRelativePath = 'dev/native-localization.mts'
function contractPathComponents(path) {
  return path.split('/')
}
function fail(message) {
  throw new Error(`Native contract staging failed: ${message}`)
}
function absoluteDirectoryPath(value, label) {
  if (typeof value !== 'string' || value.includes('\0') || value.trim() === '')
    fail(`${label} is required`)
  if (!isAbsolute(value.trim())) fail(`${label} must be an absolute path`)
  const path = resolve(value.trim())
  if (path === resolve(sep)) fail(`${label} must not be the filesystem root`)
  return path
}
async function info(path, label) {
  try {
    const result = await lstat(path)
    if (result.isSymbolicLink()) fail(`symbolic link found at ${label}`)
    return result
  } catch (error) {
    if (error.code === 'ENOENT') return undefined
    throw error
  }
}
async function realDirectory(path, label) {
  const result = await info(path, label)
  if (!result) fail(`missing directory at ${label}`)
  if (!result.isDirectory()) fail(`expected directory at ${label}`)
}
async function descendantInfo(root, child, label) {
  await realDirectory(root, `${label} root`)
  let current = root
  for (const component of contractPathComponents(child)) {
    current = join(current, component)
    const result = await info(current, `${label}/${component}`)
    if (!result) return undefined
    if (!result.isDirectory()) fail(`expected directory at ${label}/${component}`)
  }
  return true
}
async function tree(root, label) {
  await realDirectory(root, label)
  async function walk(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      if (entry.isSymbolicLink()) fail(`symbolic link found at ${label}/${entry.name}`)
      if (entry.isDirectory()) await walk(path)
      else if (!entry.isFile()) fail(`unsupported filesystem entry at ${label}/${entry.name}`)
    }
  }
  await walk(root)
}
async function paths() {
  let configuration
  try {
    configuration = JSON.parse(await readFile(configurationPath, 'utf8'))
  } catch (error) {
    fail(`cannot read contract configuration: ${error.message}`)
  }
  if (JSON.stringify(configuration.paths) !== JSON.stringify(declaredFilamentsContractPaths))
    fail('contract configuration paths do not match the client contract')
  return configuration.paths
}
function within(root, path) {
  const child = relative(root, path)
  return child !== '' && child !== '..' && !child.startsWith(`..${sep}`) && !isAbsolute(child)
}
async function emptyOutputRoot(root) {
  await realDirectory(root, 'stage output root')
  if ((await readdir(root)).length !== 0) fail('stage output root must be empty')
}
async function copyDirectory(sourceRoot, destinationRoot, path) {
  const source = join(sourceRoot, ...contractPathComponents(path))
  const destination = join(destinationRoot, ...contractPathComponents(path))
  await tree(source, `Filaments contract ${path}`)
  await mkdir(resolve(destination, '..'), { recursive: true })
  await cp(source, destination, { recursive: true, dereference: false, errorOnExist: true })
  await tree(destination, `staged contract ${path}`)
}
async function assertOnlyDeclaredPaths(root, declaredPaths) {
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const child = prefix ? `${prefix}/${entry.name}` : entry.name
      if (entry.isSymbolicLink()) fail(`symbolic link found at staged contract ${child}`)
      const permitted = declaredPaths.some(
        path => path === child || path.startsWith(`${child}/`) || child.startsWith(`${path}/`),
      )
      if (!permitted) fail(`unexpected staged contract path ${child}`)
      if (entry.isDirectory()) await walk(join(directory, entry.name), child)
      else if (!entry.isFile()) fail(`unsupported filesystem entry at staged contract ${child}`)
    }
  }
  await walk(root)
}
async function runFilamentsExporter({ filamentsRoot, consumerRoot, outputRoot }) {
  const script = join(filamentsRoot, ...contractPathComponents(filamentsExporterRelativePath))
  await new Promise((resolvePromise, reject) => {
    const child = spawn(
      process.execPath,
      [script, '--output-root', outputRoot, '--consumer-root', consumerRoot],
      { cwd: filamentsRoot, stdio: 'inherit' },
    )
    child.once('error', reject)
    child.once('exit', (code, signal) => {
      if (code === 0) resolvePromise()
      else
        reject(
          new Error(`Filaments exporter failed (${signal ? `signal ${signal}` : `exit ${code}`})`),
        )
    })
  })
}
async function hasFilamentsExporter(filamentsRoot) {
  const script = join(filamentsRoot, ...contractPathComponents(filamentsExporterRelativePath))
  if (!(await info(script, `Filaments exporter ${filamentsExporterRelativePath}`))?.isFile())
    return false
  const source = await readFile(script, 'utf8')
  return source.includes('--output-root') && source.includes('--consumer-root')
}
export async function stageNativeContract(options = {}) {
  const filamentsRoot = absoluteDirectoryPath(options.filamentsRoot, 'Filaments root')
  const consumerRoot = absoluteDirectoryPath(options.consumerRoot, 'candidate client root')
  const outputRoot = absoluteDirectoryPath(options.outputRoot, 'stage output root')
  if (
    outputRoot === filamentsRoot ||
    outputRoot === consumerRoot ||
    within(filamentsRoot, outputRoot) ||
    within(consumerRoot, outputRoot)
  )
    fail('stage output root must be isolated from source and candidate roots')
  await Promise.all([
    realDirectory(filamentsRoot, 'Filaments root'),
    realDirectory(consumerRoot, 'candidate client root'),
    emptyOutputRoot(outputRoot),
  ])
  const declaredPaths = await paths()
  const [fixturePath, ...localizationPaths] = declaredPaths
  if (!(await descendantInfo(filamentsRoot, fixturePath, 'Filaments contract')))
    fail(`missing directory at Filaments contract/${fixturePath}`)
  const legacyState = await Promise.all(
    localizationPaths.map(path => descendantInfo(filamentsRoot, path, 'Filaments contract')),
  )
  const runExporter = options.runExporter ?? runFilamentsExporter
  if (options.runExporter || (await hasFilamentsExporter(filamentsRoot))) {
    try {
      await runExporter({ consumerRoot, filamentsRoot, outputRoot })
    } catch (error) {
      fail(`Filaments exporter failed: ${error.message}`)
    }
    // Validate the exporter's complete output before creating or copying through
    // any exporter-controlled destination ancestor.
    await assertOnlyDeclaredPaths(outputRoot, declaredPaths)
    for (const path of localizationPaths) {
      if (!(await descendantInfo(outputRoot, path, 'staged contract')))
        fail(`missing directory at staged contract/${path}`)
      await tree(join(outputRoot, ...contractPathComponents(path)), `staged contract ${path}`)
    }
    await copyDirectory(filamentsRoot, outputRoot, fixturePath)
    await assertOnlyDeclaredPaths(outputRoot, declaredPaths)
    return 'exporter'
  }
  if (legacyState.some(Boolean) && !legacyState.every(Boolean))
    fail('partial legacy localization directories in Filaments checkout')
  if (legacyState.every(Boolean)) {
    for (const path of declaredPaths) await copyDirectory(filamentsRoot, outputRoot, path)
    await assertOnlyDeclaredPaths(outputRoot, declaredPaths)
    return 'legacy'
  }
  fail(`missing Filaments exporter at ${filamentsExporterRelativePath}`)
}

function parseArguments(argv) {
  if (
    argv.length !== 6 ||
    argv[0] !== '--filaments-root' ||
    argv[2] !== '--output-root' ||
    argv[4] !== '--consumer-root'
  )
    fail(
      'usage: node scripts/stage-native-contract.mjs --filaments-root <absolute-root> --output-root <absolute-root> --consumer-root <absolute-root>',
    )
  return { filamentsRoot: argv[1], outputRoot: argv[3], consumerRoot: argv[5] }
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  await stageNativeContract(parseArguments(process.argv.slice(2)))
