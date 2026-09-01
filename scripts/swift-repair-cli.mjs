import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import {
  SWIFT_ANDROID_MANIFEST_PATH,
  SWIFT_ANDROID_MATERIALIZER_PATH,
  SWIFT_ANDROID_RESOLVED_PATH,
  assertion,
  parseRawGitDiff,
  validatePublishedArtifactPaths,
  validatePublishedCommitPaths,
} from './swift-repair-common.mjs'

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
async function readJsonInput(path) {
  const source = path
    ? await readFile(resolve(path), 'utf8')
    : await new Promise((resolveInput, reject) => {
        let data = ''
        process.stdin.setEncoding('utf8')
        process.stdin.on('data', chunk => {
          data += chunk
        })
        process.stdin.on('end', () => resolveInput(data))
        process.stdin.on('error', reject)
      })
  return JSON.parse(source)
}
export async function main(argv, api) {
  const [mode = 'pull-request', ...rest] = argv
  const options = parseCli(rest)
  const input = options.input ? await readJsonInput(options.input) : null
  let result
  if (mode === 'pull-request' && rest.length >= 7 && !options.input) {
    const [pullRequestPath, rawDiffPath, defaultBranch, repository, headRef, baseSha, headSha] =
      rest
    const [pullRequest, rawDiff] = await Promise.all([
      readFile(pullRequestPath, 'utf8'),
      readFile(rawDiffPath),
    ])
    result = api.validateSwiftAndroidRepairPullRequest(
      JSON.parse(pullRequest),
      rawDiff,
      defaultBranch,
      repository,
      headRef,
      baseSha,
      headSha,
    )
  } else if (mode === 'pull-request') {
    if (input) result = api.validateInputFiles(input)
    else {
      assertion(
        options.metadata && options['raw-diff'],
        'pull-request validation requires --metadata and --raw-diff',
      )
      const metadata = await readJsonInput(options.metadata)
      const rawDiff = await readFile(resolve(options['raw-diff']), 'utf8')
      const root = options.root ?? process.cwd(),
        trustedRoot = options['trusted-root'] ?? root,
        candidateRoot = options['candidate-root'] ?? root
      const read = (base, path) => readFile(resolve(base, path), 'utf8')
      const [trustedManifest, candidateManifest, trustedResolved, candidateResolved] =
        await Promise.all([
          read(trustedRoot, SWIFT_ANDROID_MANIFEST_PATH),
          read(candidateRoot, SWIFT_ANDROID_MANIFEST_PATH),
          read(trustedRoot, SWIFT_ANDROID_RESOLVED_PATH),
          read(candidateRoot, SWIFT_ANDROID_RESOLVED_PATH),
        ])
      const changedPaths = parseRawGitDiff(rawDiff).map(entry => entry.path)
      result = api.validateInputFiles({
        metadata,
        changedPaths,
        manifest: { trusted: trustedManifest, candidate: candidateManifest },
        resolved: { trusted: trustedResolved, candidate: candidateResolved },
        candidateMaterializer: changedPaths.includes(SWIFT_ANDROID_MATERIALIZER_PATH)
          ? await read(candidateRoot, SWIFT_ANDROID_MATERIALIZER_PATH)
          : undefined,
      })
    }
  } else if (mode === 'provenance') {
    if (input) result = api.validateProvenance(input)
    else {
      assertion(
        rest.length === 6,
        'Usage: ... provenance <provenance.json> <repository> <pr> <run> <base-sha> <head-sha>',
      )
      result = api.validateSwiftAndroidRepairProvenance(
        JSON.parse(await readFile(rest[0], 'utf8')),
        ...rest.slice(1),
      )
    }
  } else if (mode === 'published-paths')
    result = validatePublishedCommitPaths(input ? (input.paths ?? input) : await readFile(rest[0]))
  else if (mode === 'artifact-paths')
    result = validatePublishedArtifactPaths(
      input ? (input.paths ?? input) : await readFile(rest[0]),
    )
  else throw new Error(`unknown validation mode: ${mode}`)
  process.stdout.write(`${JSON.stringify(await result)}\n`)
}
