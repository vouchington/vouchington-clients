import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { writeSwiftAndroidRepair } from './repair-swift-write.mjs'
import {
  parseCli,
  parseRawGitDiff,
  SWIFT_ANDROID_MATERIALIZER_PATH,
} from './swift-repair-common.mjs'

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
  const result = await writeSwiftAndroidRepair({
    outputRoot: options.output ?? input.outputRoot ?? process.cwd(),
    prepare: () =>
      api.prepareSwiftAndroidRepairFromFiles({
        ...input,
        root: input.root ?? process.cwd(),
        trustedRoot: input.trustedRoot ?? input.root ?? process.cwd(),
        candidateRoot: input.candidateRoot ?? input.root ?? process.cwd(),
      }),
  })
  process.stdout.write(
    `${JSON.stringify({ paths: [SWIFT_ANDROID_MATERIALIZER_PATH, 'dependabot-swift-android-provenance.json'], provenance: result.provenance })}\n`,
  )
}
