import { readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { validateNugetUpdate } from 'vouchington-tooling/nuget-central-version'

export function validateDependabotNugetUpdate(trustedSource, candidateSource, metadataSource) {
  return validateNugetUpdate(trustedSource, candidateSource, metadataSource)
}

export async function runDependabotNugetUpdateCli(args) {
  const [trustedPath, candidatePath, metadataPath, outputPath] = args
  if (!trustedPath || !candidatePath || !metadataPath || !outputPath || args.length !== 4) {
    throw new Error(
      'Usage: validate-dependabot-nuget-update.mjs <trusted-props> <candidate-props> <metadata-json> <output-props>',
    )
  }
  const [trustedSource, candidateSource, metadataSource] = await Promise.all([
    readFile(trustedPath, 'utf8'),
    readFile(candidatePath, 'utf8'),
    readFile(metadataPath, 'utf8'),
  ])
  const changedPackages = validateDependabotNugetUpdate(
    trustedSource,
    candidateSource,
    metadataSource,
  )
  await writeFile(outputPath, candidateSource, { encoding: 'utf8', flag: 'wx' })
  return changedPackages
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  runDependabotNugetUpdateCli(process.argv.slice(2)).catch(error => {
    process.stderr.write(`${error.message}\n`)
    process.exitCode = 1
  })
}
