import { lstat, readFile, readdir } from 'node:fs/promises'
import { join } from 'node:path'
import { pairedExporterOutputPaths } from './paired-exporter-runtime-evidence.mjs'

const maximumExportBytes = 200 * 1024 * 1024
const maximumExportFiles = pairedExporterOutputPaths.length
const localizationRoots = [
  'swift-clients/ui/Sources/VouchaLocalization/Generated',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
]

function fail(message) {
  throw new Error(`Trusted exporter bootstrap rejected input: ${message}`)
}

export async function collectPairedExporterOutput() {
  const files = []
  let total = 0
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = prefix ? `${prefix}/${entry.name}` : entry.name
      const fullPath = join(directory, entry.name)
      const info = await lstat(fullPath)
      if (info.isSymbolicLink()) fail(`export output contains a symbolic link: ${path}`)
      if (info.isDirectory()) {
        if (!pairedExporterOutputPaths.some(file => file.startsWith(`${path}/`)))
          fail(`export output contains an undeclared directory: ${path}`)
        await walk(fullPath, path)
      } else if (info.isFile()) {
        if (!pairedExporterOutputPaths.includes(path))
          fail(`export output contains a path outside the exact generated file set: ${path}`)
        if (files.length >= maximumExportFiles) fail('export output contains too many files')
        total += info.size
        if (total > maximumExportBytes) fail('export output exceeds its byte limit')
        files.push({ path, contentBase64: (await readFile(fullPath)).toString('base64') })
      } else {
        fail(`export output contains an unsupported entry: ${path}`)
      }
    }
  }
  for (const path of localizationRoots) {
    const fullRoot = join('/output', path)
    const info = await lstat(fullRoot)
    if (info.isSymbolicLink() || !info.isDirectory()) fail(`exporter omitted or replaced ${path}`)
    await walk(fullRoot, path)
  }
  const collectedPaths = new Set(files.map(file => file.path))
  if (
    files.length !== pairedExporterOutputPaths.length ||
    pairedExporterOutputPaths.some(path => !collectedPaths.has(path))
  )
    fail('exporter did not produce the exact generated localization file set')
  files.sort((left, right) => left.path.localeCompare(right.path))
  return files
}
