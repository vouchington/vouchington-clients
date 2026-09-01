import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { parseRawGitDiff, SWIFT_ANDROID_MATERIALIZER_PATH } from './swift-repair-common.mjs'

function parseCli(argv) {
  const options = {}
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index]
    if (!argument.startsWith('--')) continue
    const key = argument.slice(2)
    options[key] = argv[index + 1]?.startsWith('--') ? true : argv[++index]
  }
  return options
}
const readJson = async path => JSON.parse(await readFile(resolve(path), 'utf8'))
export async function main(argv, api) {
  const options = parseCli(argv)
  let input
  if (options.input) input = await readJson(options.input)
  else if (argv.length === 5 && argv.every(argument => !argument.startsWith('--'))) {
    const [trustedRoot, candidateRoot, metadataPath, rawDiffPath, outputRoot] = argv
    input = {
      metadata: await readJson(metadataPath),
      changedPaths: parseRawGitDiff(await readFile(resolve(rawDiffPath), 'utf8')).map(
        entry => entry.path,
      ),
      trustedRoot,
      candidateRoot,
      outputRoot,
    }
  } else {
    if (!options.metadata || !options['raw-diff'])
      throw new Error('repair requires --metadata and --raw-diff')
    input = {
      metadata: await readJson(options.metadata),
      changedPaths: parseRawGitDiff(await readFile(resolve(options['raw-diff']), 'utf8')).map(
        entry => entry.path,
      ),
      trustedRoot: options['trusted-root'] ?? process.cwd(),
      candidateRoot: options['candidate-root'] ?? process.cwd(),
      fetchImpl: globalThis.fetch,
    }
  }
  const result = await api.prepareSwiftAndroidRepairFromFiles({
    ...input,
    root: input.root ?? process.cwd(),
    trustedRoot: input.trustedRoot ?? input.root ?? process.cwd(),
    candidateRoot: input.candidateRoot ?? input.root ?? process.cwd(),
  })
  const outputRoot = options.output ?? input.outputRoot ?? process.cwd()
  const materializerPath = resolve(outputRoot, SWIFT_ANDROID_MATERIALIZER_PATH)
  await mkdir(dirname(materializerPath), { recursive: true })
  await writeFile(materializerPath, result.materializer)
  await writeFile(
    resolve(outputRoot, 'dependabot-swift-android-provenance.json'),
    `${JSON.stringify(result.provenance, null, 2)}\n`,
  )
  process.stdout.write(
    `${JSON.stringify({ paths: [SWIFT_ANDROID_MATERIALIZER_PATH, 'dependabot-swift-android-provenance.json'], provenance: result.provenance })}\n`,
  )
}
