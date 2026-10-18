import { lstat, readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
import { declaredFilamentsContractPaths } from './contracts.mjs'
import {
  absoluteDirectoryPath,
  assertOnlyDeclaredPaths,
  contractPathComponents,
  copyDirectory,
  declaredPaths as loadDeclaredPaths,
  descendantInfo,
  emptyOutputRoot,
  pathsOverlap,
  realDirectory,
  tree,
} from './stage-native-contract-files.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const configurationPath = join(repositoryRoot, 'contracts/filaments.json')
const filamentsExporterRelativePath = 'dev/native-localization.mts'
function fail(message) {
  throw new Error(`Native contract staging failed: ${message}`)
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
  const exporterOutputRoot = options.exporterOutputRoot
    ? absoluteDirectoryPath(options.exporterOutputRoot, 'exporter output root')
    : undefined
  if (
    outputRoot === filamentsRoot ||
    outputRoot === consumerRoot ||
    pathsOverlap(filamentsRoot, outputRoot) ||
    pathsOverlap(consumerRoot, outputRoot)
  )
    fail('stage output root must be isolated from source and candidate roots')
  if (
    exporterOutputRoot &&
    [filamentsRoot, consumerRoot, outputRoot].some(root => pathsOverlap(root, exporterOutputRoot))
  )
    fail('exporter output root must be isolated from source and stage roots')
  await Promise.all([
    realDirectory(filamentsRoot, 'Filaments root'),
    realDirectory(consumerRoot, 'candidate client root'),
    emptyOutputRoot(outputRoot),
  ])
  const paths = await loadDeclaredPaths(configurationPath, declaredFilamentsContractPaths)
  const [fixturePath, ...localizationPaths] = paths
  if (!(await descendantInfo(filamentsRoot, fixturePath, 'Filaments contract')))
    fail(`missing directory at Filaments contract/${fixturePath}`)
  const legacyState = await Promise.all(
    localizationPaths.map(path => descendantInfo(filamentsRoot, path, 'Filaments contract')),
  )
  if (exporterOutputRoot) {
    await realDirectory(exporterOutputRoot, 'exporter output root')
    await assertOnlyDeclaredPaths(exporterOutputRoot, localizationPaths)
    for (const path of localizationPaths) {
      if (!(await descendantInfo(exporterOutputRoot, path, 'isolated exporter output')))
        fail(`missing directory at isolated exporter output/${path}`)
      await tree(
        join(exporterOutputRoot, ...contractPathComponents(path)),
        `isolated exporter output ${path}`,
      )
    }
    await copyDirectory(filamentsRoot, outputRoot, fixturePath)
    for (const path of localizationPaths) await copyDirectory(exporterOutputRoot, outputRoot, path)
    await assertOnlyDeclaredPaths(outputRoot, paths)
    return 'isolated-exporter-data'
  }
  const runExporter = options.runExporter ?? runFilamentsExporter
  if (options.runExporter || (await hasFilamentsExporter(filamentsRoot))) {
    try {
      await runExporter({ consumerRoot, filamentsRoot, outputRoot })
    } catch (error) {
      fail(`Filaments exporter failed: ${error.message}`)
    }
    // Validate the exporter's complete output before creating or copying through
    // any exporter-controlled destination ancestor.
    await assertOnlyDeclaredPaths(outputRoot, paths)
    for (const path of localizationPaths) {
      if (!(await descendantInfo(outputRoot, path, 'staged contract')))
        fail(`missing directory at staged contract/${path}`)
      await tree(join(outputRoot, ...contractPathComponents(path)), `staged contract ${path}`)
    }
    await copyDirectory(filamentsRoot, outputRoot, fixturePath)
    await assertOnlyDeclaredPaths(outputRoot, paths)
    return 'exporter'
  }
  if (legacyState.some(Boolean) && !legacyState.every(Boolean))
    fail('partial legacy localization directories in Filaments checkout')
  if (legacyState.every(Boolean)) {
    for (const path of paths) await copyDirectory(filamentsRoot, outputRoot, path)
    await assertOnlyDeclaredPaths(outputRoot, paths)
    return 'legacy'
  }
  fail(`missing Filaments exporter at ${filamentsExporterRelativePath}`)
}

function parseArguments(argv) {
  if (argv.length !== 6 && argv.length !== 8)
    fail(
      'usage: node scripts/stage-native-contract.mjs --filaments-root <absolute-root> --output-root <absolute-root> --consumer-root <absolute-root> [--exporter-output-root <absolute-root>]',
    )
  if (
    argv[0] !== '--filaments-root' ||
    argv[2] !== '--output-root' ||
    argv[4] !== '--consumer-root'
  )
    fail('invalid contract staging arguments')
  if (argv.length === 8 && argv[6] !== '--exporter-output-root')
    fail('invalid isolated exporter output argument')
  return {
    filamentsRoot: argv[1],
    outputRoot: argv[3],
    consumerRoot: argv[5],
    ...(argv.length === 8 ? { exporterOutputRoot: argv[7] } : {}),
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  await stageNativeContract(parseArguments(process.argv.slice(2)))
