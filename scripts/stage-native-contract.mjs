import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
import { contractDataPaths, declaredContractPaths, localizationPaths } from './contracts.mjs'
import {
  absoluteDirectoryPath,
  assertOnlyDeclaredPaths,
  contractPathComponents,
  copyDirectory,
  declaredPaths as loadDeclaredPaths,
  descendantInfo,
  emptyOutputRoot,
  info,
  pathsOverlap,
  realDirectory,
  tree,
} from './stage-native-contract-files.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const configurationPath = join(repositoryRoot, 'contracts/vouchington.json')
const vouchingtonExporterRelativePath = 'dev/native-localization.mts'
const fail = prefixedFailure('Native contract staging failed')
async function runVouchingtonExporter({ vouchingtonRoot, consumerRoot, outputRoot }) {
  const script = join(vouchingtonRoot, ...contractPathComponents(vouchingtonExporterRelativePath))
  await new Promise((resolvePromise, reject) => {
    const child = spawn(
      process.execPath,
      [script, '--output-root', outputRoot, '--consumer-root', consumerRoot],
      { cwd: vouchingtonRoot, stdio: 'inherit' },
    )
    child.once('error', reject)
    child.once('exit', (code, signal) => {
      if (code === 0) resolvePromise()
      else
        reject(
          new Error(
            `Vouchington exporter failed (${signal ? `signal ${signal}` : `exit ${code}`})`,
          ),
        )
    })
  })
}
async function hasVouchingtonExporter(vouchingtonRoot) {
  const script = join(vouchingtonRoot, ...contractPathComponents(vouchingtonExporterRelativePath))
  if (!(await info(script, `Vouchington exporter ${vouchingtonExporterRelativePath}`))?.isFile())
    return false
  const source = await readFile(script, 'utf8')
  return source.includes('--output-root') && source.includes('--consumer-root')
}
export async function stageNativeContract(options = {}) {
  const vouchingtonRoot = absoluteDirectoryPath(options.vouchingtonRoot, 'Vouchington root')
  const consumerRoot = absoluteDirectoryPath(options.consumerRoot, 'candidate client root')
  const outputRoot = absoluteDirectoryPath(options.outputRoot, 'stage output root')
  const exporterOutputRoot = options.exporterOutputRoot
    ? absoluteDirectoryPath(options.exporterOutputRoot, 'exporter output root')
    : undefined
  if (
    outputRoot === vouchingtonRoot ||
    outputRoot === consumerRoot ||
    pathsOverlap(vouchingtonRoot, outputRoot) ||
    pathsOverlap(consumerRoot, outputRoot)
  )
    fail('stage output root must be isolated from source and candidate roots')
  if (
    exporterOutputRoot &&
    [vouchingtonRoot, consumerRoot, outputRoot].some(root => pathsOverlap(root, exporterOutputRoot))
  )
    fail('exporter output root must be isolated from source and stage roots')
  await Promise.all([
    realDirectory(vouchingtonRoot, 'Vouchington root'),
    realDirectory(consumerRoot, 'candidate client root'),
    emptyOutputRoot(outputRoot),
  ])
  const paths = await loadDeclaredPaths(configurationPath, declaredContractPaths)
  for (const path of contractDataPaths)
    if (!(await descendantInfo(vouchingtonRoot, path, 'Vouchington contract')))
      fail(`missing directory at Vouchington contract/${path}`)
  const legacyState = await Promise.all(
    localizationPaths.map(path => descendantInfo(vouchingtonRoot, path, 'Vouchington contract')),
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
    for (const path of contractDataPaths) await copyDirectory(vouchingtonRoot, outputRoot, path)
    for (const path of localizationPaths) await copyDirectory(exporterOutputRoot, outputRoot, path)
    await assertOnlyDeclaredPaths(outputRoot, paths)
    return 'isolated-exporter-data'
  }
  const runExporter = options.runExporter ?? runVouchingtonExporter
  if (options.runExporter || (await hasVouchingtonExporter(vouchingtonRoot))) {
    try {
      await runExporter({ consumerRoot, vouchingtonRoot, outputRoot })
    } catch (error) {
      fail(`Vouchington exporter failed: ${error.message}`)
    }
    // Validate the exporter's complete output before creating or copying through
    // any exporter-controlled destination ancestor.
    await assertOnlyDeclaredPaths(outputRoot, localizationPaths)
    for (const path of localizationPaths) {
      if (!(await descendantInfo(outputRoot, path, 'staged contract')))
        fail(`missing directory at staged contract/${path}`)
      await tree(join(outputRoot, ...contractPathComponents(path)), `staged contract ${path}`)
    }
    for (const path of contractDataPaths) await copyDirectory(vouchingtonRoot, outputRoot, path)
    await assertOnlyDeclaredPaths(outputRoot, paths)
    return 'exporter'
  }
  if (legacyState.some(Boolean) && !legacyState.every(Boolean))
    fail('partial legacy localization directories in Vouchington checkout')
  if (legacyState.every(Boolean)) {
    for (const path of paths) await copyDirectory(vouchingtonRoot, outputRoot, path)
    await assertOnlyDeclaredPaths(outputRoot, paths)
    return 'legacy'
  }
  fail(`missing Vouchington exporter at ${vouchingtonExporterRelativePath}`)
}

function parseArguments(argv) {
  if (argv.length !== 6 && argv.length !== 8)
    fail(
      'usage: node scripts/stage-native-contract.mjs --vouchington-root <absolute-root> --output-root <absolute-root> --consumer-root <absolute-root> [--exporter-output-root <absolute-root>]',
    )
  if (
    argv[0] !== '--vouchington-root' ||
    argv[2] !== '--output-root' ||
    argv[4] !== '--consumer-root'
  )
    fail('invalid contract staging arguments')
  if (argv.length === 8 && argv[6] !== '--exporter-output-root')
    fail('invalid isolated exporter output argument')
  return {
    vouchingtonRoot: argv[1],
    outputRoot: argv[3],
    consumerRoot: argv[5],
    ...(argv.length === 8 ? { exporterOutputRoot: argv[7] } : {}),
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url))
  await stageNativeContract(parseArguments(process.argv.slice(2)))
