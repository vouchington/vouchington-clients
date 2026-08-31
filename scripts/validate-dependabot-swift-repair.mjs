import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { validateResolvedPinDelta } from 'vouchington-tooling/swift-resolved-pin-delta'

export const SWIFT_ANDROID_MANIFEST_PATH = 'swift-clients/apps/android/Package.swift'
export const SWIFT_ANDROID_RESOLVED_PATH = 'swift-clients/apps/android/Package.resolved'
export const SWIFT_ANDROID_MATERIALIZER_PATH =
  'swift-clients/apps/android/tooling/materialize-skip-sdk.sh'

// Dependabot is allowed to update the package manifest and lock. The materializer is a
// trusted-base input to the repair job and is never taken from the pull-request checkout.
export const SWIFT_ANDROID_PULL_REQUEST_PATHS = Object.freeze([
  SWIFT_ANDROID_MANIFEST_PATH,
  SWIFT_ANDROID_RESOLVED_PATH,
])
export const SWIFT_ANDROID_PULL_REQUEST_PATH_SETS = Object.freeze([
  Object.freeze([SWIFT_ANDROID_RESOLVED_PATH]),
  Object.freeze([SWIFT_ANDROID_MANIFEST_PATH, SWIFT_ANDROID_RESOLVED_PATH]),
  Object.freeze([
    SWIFT_ANDROID_MANIFEST_PATH,
    SWIFT_ANDROID_RESOLVED_PATH,
    SWIFT_ANDROID_MATERIALIZER_PATH,
  ]),
])
export const SWIFT_ANDROID_ARTIFACT_PATHS = Object.freeze([
  SWIFT_ANDROID_MATERIALIZER_PATH,
  'dependabot-swift-android-provenance.json',
])

const SHA256 = /^[a-f0-9]{64}$/u
const SHA1 = /^[a-f0-9]{40}$/u
const SEMVER = /^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/u
const REVISION = /^[a-f0-9]{40}$/u
const SKIP_URL = 'https://source.skip.tools/skip.git'

function assertion(condition, message) {
  if (!condition) throw new Error(message)
}

function uniqueValues(values, label) {
  assertion(Array.isArray(values), `${label} must be an array`)
  const result = [...new Set(values)]
  assertion(result.length === values.length, `${label} must not contain duplicates`)
  return result
}

export function validateChangedPaths(paths, allowed = SWIFT_ANDROID_PULL_REQUEST_PATHS) {
  const actual = uniqueValues(paths, 'changed paths').toSorted()
  const expected = uniqueValues(allowed, 'allowed paths').toSorted()
  assertion(
    actual.length === expected.length && actual.every((path, index) => path === expected[index]),
    `unexpected Swift Android repair paths (expected ${expected.join(', ')}, got ${actual.join(', ')})`,
  )
  return actual
}

export function validatePullRequestPaths(paths) {
  const actual = uniqueValues(paths, 'changed paths').toSorted()
  const accepted = SWIFT_ANDROID_PULL_REQUEST_PATH_SETS.some(expected => {
    const sorted = expected.toSorted()
    return actual.length === sorted.length && actual.every((path, index) => path === sorted[index])
  })
  assertion(accepted, `unexpected Swift Android pull-request paths (got ${actual.join(', ')})`)
  return actual
}

export function validatePublishedCommitPaths(paths) {
  if (typeof paths === 'string' || Buffer.isBuffer(paths)) {
    const source = Buffer.isBuffer(paths) ? paths.toString('utf8') : paths
    assertion(source.endsWith('\0'), 'published paths must be NUL-delimited')
    paths = source.slice(0, -1).split('\0')
  }
  return validateChangedPaths(paths, [SWIFT_ANDROID_MATERIALIZER_PATH])
}

export function validatePublishedArtifactPaths(paths) {
  let names = paths
  if (!Array.isArray(paths) && paths && typeof paths === 'object') names = paths.paths
  if (typeof names === 'string' || Buffer.isBuffer(names)) {
    const source = Buffer.isBuffer(names) ? names.toString('utf8') : names
    assertion(source.endsWith('\0'), 'published paths must be NUL-delimited')
    names = source.slice(0, -1).split('\0')
  }
  return validateChangedPaths(names, SWIFT_ANDROID_ARTIFACT_PATHS)
}

// Backwards-compatible name used by callers that validate the two-file artifact.
export const validatePublishedPaths = validatePublishedArtifactPaths

export function parseRawGitDiff(raw) {
  assertion(typeof raw === 'string', 'raw git diff is required')
  const fields = raw.split('\0')
  const entries = []
  for (let index = 0; index < fields.length;) {
    const header = fields[index++]
    if (header === '') continue
    assertion(header.startsWith(':'), 'raw git diff contains an invalid record')
    const parts = header.slice(1).split(' ')
    assertion(parts.length === 5, 'raw git diff record is malformed')
    const [oldMode, newMode, oldSha, newSha, status] = parts
    assertion(oldMode === '100644' && newMode === '100644', 'Swift repair accepts regular files only')
    assertion(/^[0-9a-f]{7,64}$/u.test(oldSha) && /^[0-9a-f]{7,64}$/u.test(newSha), 'raw git diff contains an invalid object id')
    assertion(status === 'M', `raw git diff status ${status} is not an in-place modification`)
    const path = fields[index++]
    assertion(path && !path.includes('\0') && !path.startsWith('/') && !path.includes('..'), 'raw git diff contains an invalid path')
    entries.push({ oldMode, newMode, oldSha, newSha, status, path })
  }
  assertion(entries.length > 0, 'raw git diff is empty')
  return entries
}

export function validateDependabotPullRequest(metadata) {
  assertion(metadata && typeof metadata === 'object', 'pull-request metadata is required')
  const pullRequest = metadata.pull_request ?? metadata
  const repository = metadata.repository ?? {}
  assertion(pullRequest.user?.login === 'dependabot[bot]', 'pull request is not authored by Dependabot')
  assertion(
    pullRequest.base?.ref === (repository.default_branch ?? pullRequest.base?.repo?.default_branch ?? 'main'),
    'pull request must target the repository default branch',
  )
  const repositoryName = repository.full_name ?? pullRequest.base?.repo?.full_name
  assertion(repositoryName && pullRequest.head?.repo?.full_name === repositoryName, 'pull request head must be in this repository')
  assertion(/^dependabot\//u.test(pullRequest.head?.ref ?? ''), 'pull request head must be a Dependabot branch')
  assertion(SHA1.test(pullRequest.base?.sha ?? ''), 'pull request base SHA must be a full commit SHA')
  assertion(SHA1.test(pullRequest.head?.sha ?? ''), 'pull request head SHA must be a full commit SHA')
  return {
    baseSha: pullRequest.base.sha,
    headSha: pullRequest.head.sha,
    headRef: pullRequest.head.ref,
    repository: repositoryName,
    pullRequestNumber: pullRequest.number,
    runId: metadata.run_id ?? metadata.runId,
  }
}

export function validateSwiftAndroidRepairRawDiff(rawDiff) {
  const entries = parseRawGitDiff(rawDiff)
  const changedPaths = entries.map(entry => entry.path)
  validatePullRequestPaths(changedPaths)
  return changedPaths
}

export function validateSwiftAndroidRepairPullRequest(
  pullRequest,
  rawDiff,
  defaultBranch,
  repository,
  headRef,
  baseSha,
  headSha,
) {
  assertion(typeof defaultBranch === 'string' && defaultBranch.length > 0, 'default branch must be nonempty')
  assertion(typeof repository === 'string' && /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/u.test(repository), 'repository must be an owner/name repository')
  assertion(SHA1.test(baseSha), 'base SHA must be a lowercase 40-character Git SHA')
  assertion(SHA1.test(headSha), 'head SHA must be a lowercase 40-character Git SHA')
  assertion(typeof headRef === 'string' && headRef.startsWith('dependabot/'), 'head ref must be a Dependabot branch')
  const live = pullRequest
  assertion(live && typeof live === 'object', 'pull request must be an object')
  assertion(live.user?.login === 'dependabot[bot]', 'pull request author must be Dependabot')
  assertion(live.state === 'open', 'pull request must be open')
  assertion(live.draft === false, 'pull request must not be a draft')
  assertion(live.base?.ref === defaultBranch, 'pull request base branch did not match trusted context')
  assertion(live.base?.sha === baseSha, 'pull request base SHA did not match trusted context')
  assertion(live.base?.repo?.full_name === repository, 'pull request base repository did not match trusted context')
  assertion(live.head?.ref === headRef, 'pull request head ref did not match trusted context')
  assertion(live.head?.sha === headSha, 'pull request head SHA did not match trusted context')
  assertion(live.head?.repo?.full_name === repository, 'pull request head repository did not match trusted context')
  const changedPaths = validateSwiftAndroidRepairRawDiff(rawDiff)
  assertion(live.changed_files === changedPaths.length, 'pull request changed_files did not match raw diff')
  return Object.freeze({ number: live.number, baseSha, headSha, changedPaths })
}

function skipDependencyMatches(source) {
  assertion(typeof source === 'string', 'Package.swift source must be a string')
  // Keep this deliberately narrow: a commented-out or second Skip dependency must not be
  // mistaken for the active dependency that Dependabot is updating.
  const expression = /^\s*\.package\s*\(\s*url\s*:\s*"(https:\/\/source\.skip\.tools\/skip\.git)"\s*,\s*exact\s*:\s*"([^"]+)"\s*\)\s*,?\s*$/gmu
  return [...source.matchAll(expression)].filter(match => !/^[ \t]*\/\//u.test(match[0]))
}

export function parseSkipDependency(source) {
  const matches = skipDependencyMatches(source)
  assertion(matches.length === 1, 'Package.swift must contain exactly one active Skip exact dependency')
  const match = matches[0]
  assertion(match[1] === SKIP_URL && SEMVER.test(match[2]), 'Skip dependency must use an exact stable semver')
  return { version: match[2], start: match.index, end: match.index + match[0].length, text: match[0] }
}

export function validateSkipManifestDelta(trustedSource, candidateSource) {
  const trusted = parseSkipDependency(trustedSource)
  const candidate = parseSkipDependency(candidateSource)
  assertion(trusted.version !== candidate.version, 'Dependabot Swift repair must update the Skip version')
  const trustedCanonical = trustedSource.replace(`exact: "${trusted.version}"`, `exact: "${candidate.version}"`)
  assertion(trustedCanonical === candidateSource, 'Package.swift may change only the exact Skip version')
  return { trustedVersion: trusted.version, candidateVersion: candidate.version }
}

export function parseResolvedDocument(source) {
  if (typeof source === 'string') {
    try {
      return JSON.parse(source)
    } catch (error) {
      throw new Error(`Package.resolved is not valid JSON: ${error.message}`)
    }
  }
  assertion(source && typeof source === 'object', 'Package.resolved document is required')
  return source
}

export function validateSkipResolvedDelta(trustedSource, candidateSource, expectedVersion, { requireVersionChange = true } = {}) {
  const trusted = parseResolvedDocument(trustedSource)
  const candidate = parseResolvedDocument(candidateSource)
  validateResolvedPinDelta(trusted, candidate, { requiredIdentity: 'skip' })
  const trustedPin = trusted.pins.find(pin => pin.identity === 'skip')
  const candidatePin = candidate.pins.find(pin => pin.identity === 'skip')
  assertion(trustedPin && candidatePin, 'Package.resolved must contain the Skip pin')
  assertion(candidatePin.location === SKIP_URL, 'Package.resolved must retain the trusted Skip source')
  assertion(candidatePin.state.version === expectedVersion, 'Skip lock version must match Package.swift')
  if (requireVersionChange)
    assertion(trustedPin.state.version !== candidatePin.state.version, 'Skip lock version must change')
  assertion(trustedPin.state.revision !== candidatePin.state.revision, 'Skip lock revision must change')
  assertion(REVISION.test(candidatePin.state.revision), 'Skip lock revision must be a full commit SHA')
  return {
    trustedVersion: trustedPin.state.version,
    candidateVersion: candidatePin.state.version,
    trustedRevision: trustedPin.state.revision,
    candidateRevision: candidatePin.state.revision,
    candidate,
  }
}

export function validateMaterializerSource(source) {
  assertion(typeof source === 'string', 'materializer source must be a string')
  const versionMatches = [...source.matchAll(/^SKIP_VERSION="([^"]+)"$/gmu)]
  const checksumMatches = [...source.matchAll(/^SKIP_MACOS_ZIP_SHA256="([a-f0-9]{64})"$/gmu)]
  assertion(versionMatches.length === 1 && SEMVER.test(versionMatches[0][1]), 'materializer must have one stable SKIP_VERSION')
  assertion(checksumMatches.length === 1, 'materializer must have one SKIP_MACOS_ZIP_SHA256')
  return { version: versionMatches[0][1], checksum: checksumMatches[0][1] }
}

export function validateProvenance(provenance) {
  assertion(provenance && typeof provenance === 'object', 'provenance must be an object')
  const expectedKeys = [
    'archiveChecksum', 'archiveSha256', 'archiveUrl', 'baseSha', 'headSha',
    'manifestSha256', 'materializerSha256', 'mode', 'pullRequest', 'repository',
    'resolvedSha256', 'skipRevision', 'skipVersion', 'version', 'workflowRun',
  ]
  assertion(
    Object.keys(provenance).toSorted().join('\0') === expectedKeys.toSorted().join('\0'),
    'Swift Android provenance fields do not match the supported schema',
  )
  assertion(provenance.version === 1, 'unsupported Swift Android provenance schema')
  assertion(typeof provenance.repository === 'string' && provenance.repository.length > 0, 'provenance repository is required')
  assertion(typeof provenance.pullRequest === 'number' && Number.isSafeInteger(provenance.pullRequest) && provenance.pullRequest > 0, 'provenance pull request number is required')
  assertion(typeof provenance.workflowRun === 'number' && Number.isSafeInteger(provenance.workflowRun) && provenance.workflowRun > 0, 'provenance workflow run is required')
  assertion(provenance.mode === 'swift-android', 'provenance mode is invalid')
  assertion(SHA1.test(provenance.baseSha), 'provenance baseSha must be a full commit SHA')
  assertion(SHA1.test(provenance.headSha), 'provenance headSha must be a full commit SHA')
  assertion(SEMVER.test(provenance.skipVersion), 'provenance Skip version is invalid')
  assertion(REVISION.test(provenance.skipRevision), 'provenance Skip revision is invalid')
  assertion(typeof provenance.archiveUrl === 'string' && provenance.archiveUrl === `https://github.com/skiptools/skip/releases/download/${provenance.skipVersion}/skip-macos.zip`, 'provenance archive URL is not the resolved Skip release')
  assertion(SHA256.test(provenance.archiveSha256), 'provenance archive SHA-256 is invalid')
  assertion(SHA256.test(provenance.archiveChecksum), 'provenance archive checksum is invalid')
  assertion(provenance.archiveSha256 === provenance.archiveChecksum, 'provenance archive hash does not match the Skip checksum')
  for (const name of ['manifestSha256', 'resolvedSha256', 'materializerSha256'])
    assertion(SHA256.test(provenance[name]), `provenance ${name} is invalid`)
  return provenance
}

export function validateSwiftAndroidRepairProvenance(
  provenance,
  repository,
  pullRequest,
  workflowRun,
  baseSha,
  headSha,
) {
  const value = validateProvenance(provenance)
  assertion(Number.isSafeInteger(Number(pullRequest)) && Number(pullRequest) > 0, 'pull request number must be a positive integer')
  assertion(Number.isSafeInteger(Number(workflowRun)) && Number(workflowRun) > 0, 'workflow run must be a positive integer')
  assertion(SHA1.test(baseSha) && SHA1.test(headSha), 'provenance context requires full commit SHAs')
  assertion(value.repository === repository, 'provenance repository did not match trusted context')
  assertion(value.pullRequest === Number(pullRequest), 'provenance pull request did not match trusted context')
  assertion(value.workflowRun === Number(workflowRun), 'provenance workflow run did not match trusted context')
  assertion(value.baseSha === baseSha, 'provenance base SHA did not match trusted context')
  assertion(value.headSha === headSha, 'provenance head SHA did not match trusted context')
  return value
}

export async function validateInputFiles({ root = process.cwd(), metadata, changedPaths, manifest, resolved, candidateMaterializer }) {
  const read = path => readFile(resolve(root, path), 'utf8')
  const [manifestSource, resolvedSource] = await Promise.all([
    manifest ?? read(SWIFT_ANDROID_MANIFEST_PATH),
    resolved ?? read(SWIFT_ANDROID_RESOLVED_PATH),
  ])
  const pr = validateDependabotPullRequest(metadata)
  const paths = validatePullRequestPaths(changedPaths)
  const hasManifest = paths.includes(SWIFT_ANDROID_MANIFEST_PATH)
  const manifestDelta = hasManifest
    ? validateSkipManifestDelta(manifestSource.trusted, manifestSource.candidate)
    : {
        trustedVersion: parseSkipDependency(manifestSource.trusted).version,
        candidateVersion: parseSkipDependency(manifestSource.candidate).version,
      }
  if (!hasManifest)
    assertion(manifestSource.trusted === manifestSource.candidate, 'Package.swift must remain unchanged for a lock-only repair')
  const resolvedDelta = validateSkipResolvedDelta(
    resolvedSource.trusted,
    resolvedSource.candidate,
    manifestDelta.candidateVersion,
    { requireVersionChange: hasManifest },
  )
  if (paths.includes(SWIFT_ANDROID_MATERIALIZER_PATH)) {
    assertion(candidateMaterializer, 'changed materializer source is required for path validation')
    const materializer = validateMaterializerSource(candidateMaterializer)
    assertion(materializer.version === manifestDelta.candidateVersion, 'changed materializer version must match Package.swift')
  }
  return { ...pr, ...manifestDelta, ...resolvedDelta }
}

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
  const source = path ? await readFile(resolve(path), 'utf8') : await new Promise((resolveInput, reject) => {
    let data = ''
    process.stdin.setEncoding('utf8')
    process.stdin.on('data', chunk => { data += chunk })
    process.stdin.on('end', () => resolveInput(data))
    process.stdin.on('error', reject)
  })
  return JSON.parse(source)
}

async function main(argv) {
  const [mode = 'pull-request', ...rest] = argv
  const options = parseCli(rest)
  const input = options.input ? await readJsonInput(options.input) : null
  let result
  if (mode === 'pull-request' && rest.length >= 7 && !options.input) {
    const [pullRequestPath, rawDiffPath, defaultBranch, repository, headRef, baseSha, headSha] = rest
    const [pullRequest, rawDiff] = await Promise.all([readFile(pullRequestPath, 'utf8'), readFile(rawDiffPath)])
    result = validateSwiftAndroidRepairPullRequest(JSON.parse(pullRequest), rawDiff, defaultBranch, repository, headRef, baseSha, headSha)
  } else if (mode === 'pull-request') {
    if (input) {
      result = validateInputFiles(input)
    } else {
      assertion(options.metadata && options['raw-diff'], 'pull-request validation requires --metadata and --raw-diff')
      const metadata = await readJsonInput(options.metadata)
      const rawDiff = await readFile(resolve(options['raw-diff']), 'utf8')
      const root = options.root ?? process.cwd()
      const trustedRoot = options['trusted-root'] ?? root
      const candidateRoot = options['candidate-root'] ?? root
      const read = (base, path) => readFile(resolve(base, path), 'utf8')
      const [trustedManifest, candidateManifest, trustedResolved, candidateResolved] = await Promise.all([
        read(trustedRoot, SWIFT_ANDROID_MANIFEST_PATH),
        read(candidateRoot, SWIFT_ANDROID_MANIFEST_PATH),
        read(trustedRoot, SWIFT_ANDROID_RESOLVED_PATH),
        read(candidateRoot, SWIFT_ANDROID_RESOLVED_PATH),
      ])
      const changedPaths = parseRawGitDiff(rawDiff).map(entry => entry.path)
      result = validateInputFiles({
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
    if (input) result = validateProvenance(input)
    else {
      assertion(rest.length === 6, 'Usage: ... provenance <provenance.json> <repository> <pr> <run> <base-sha> <head-sha>')
      result = validateSwiftAndroidRepairProvenance(JSON.parse(await readFile(rest[0], 'utf8')), ...rest.slice(1))
    }
  } else if (mode === 'published-paths') {
    const paths = input ? (input.paths ?? input) : await readFile(rest[0])
    result = validatePublishedCommitPaths(paths)
  } else if (mode === 'artifact-paths') {
    const paths = input ? (input.paths ?? input) : await readFile(rest[0])
    result = validatePublishedArtifactPaths(paths)
  } else {
    throw new Error(`unknown validation mode: ${mode}`)
  }
  process.stdout.write(`${JSON.stringify(await result)}\n`)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2)).catch(error => {
    process.stderr.write(`::error::${error.message}\n`)
    process.exitCode = 1
  })
}
