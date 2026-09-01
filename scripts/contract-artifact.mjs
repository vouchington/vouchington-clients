import { copyFile, mkdir, readFile, readdir, writeFile } from 'node:fs/promises'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  absolutePath,
  artifactPath,
  checkExpected,
  configuredPaths,
  createMetadata,
  fail,
  validateManifest,
} from './contract-artifact-validation.mjs'
import {
  descendantDirectory,
  fileRecord,
  info,
  treeFiles,
  verifyFiles,
} from './contract-artifact-fs.mjs'

const repositoryRoot = resolve(import.meta.dirname, '..')
const configPath = join(repositoryRoot, 'contracts/filaments.json')

export async function createContractArtifact(options) {
  const config = await configuredPaths(configPath)
  const filamentsRoot = absolutePath(options?.filamentsRoot, 'Filaments root')
  const outputRoot = absolutePath(options?.outputRoot, 'artifact output root')
  const metadata = createMetadata(options ?? {}, config)
  const existingOutput = await info(outputRoot, 'artifact output root')
  if (existingOutput) {
    if (!existingOutput.isDirectory()) fail('artifact output root must be a directory')
    if ((await readdir(outputRoot)).length !== 0) fail('artifact output root must be empty')
  }
  const records = []
  for (const allowed of config.paths) {
    const source = await descendantDirectory(filamentsRoot, allowed, 'Filaments contract')
    for (const file of await treeFiles(source, `Filaments contract ${allowed}`))
      records.push(await fileRecord(filamentsRoot, artifactPath(`${allowed}/${file}`)))
  }
  records.sort((left, right) => (left.path < right.path ? -1 : left.path > right.path ? 1 : 0))
  await mkdir(outputRoot, { recursive: true })
  for (const record of records) {
    const destination = join(outputRoot, ...record.path.split('/'))
    await mkdir(dirname(destination), { recursive: true })
    await copyFile(join(filamentsRoot, ...record.path.split('/')), destination)
  }
  const manifest = { ...metadata, allowlistedPaths: [...config.paths].sort(), files: records }
  await writeFile(join(outputRoot, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`)
  return manifest
}

export async function verifyContractArtifact(options) {
  const config = await configuredPaths(configPath)
  const root = absolutePath(options?.artifactRoot, 'artifact root')
  let manifest
  try {
    manifest = JSON.parse(await readFile(join(root, 'manifest.json'), 'utf8'))
  } catch (error) {
    fail(`cannot read manifest: ${error.message}`)
  }
  validateManifest(manifest, config)
  checkExpected(manifest, options ?? {})
  await verifyFiles(root, manifest)
  return manifest
}

function parseArguments(argv) {
  const [command, ...flags] = argv
  if (!['create', 'verify'].includes(command) || flags.length % 2 !== 0)
    fail('invalid command arguments')
  const values = {}
  for (let index = 0; index < flags.length; index += 2) {
    const flag = flags[index],
      value = flags[index + 1]
    if (!flag.startsWith('--') || value === undefined || Object.hasOwn(values, flag))
      fail('invalid command arguments')
    values[flag.slice(2).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase())] = value
  }
  return { command, values }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const { command, values } = parseArguments(process.argv.slice(2))
  if (command === 'create') await createContractArtifact(values)
  else await verifyContractArtifact(values)
}
