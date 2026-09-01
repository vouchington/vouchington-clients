import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'

import { validateResolvedPinDelta } from 'vouchington-tooling/swift-resolved-pin-delta'
import {
  assertion,
  REVISION,
  SEMVER,
  SKIP_URL,
  SWIFT_ANDROID_MANIFEST_PATH,
  SWIFT_ANDROID_MATERIALIZER_PATH,
  SWIFT_ANDROID_RESOLVED_PATH,
  validateDependabotPullRequest,
  validatePullRequestPaths,
} from './swift-repair-common.mjs'

function skipDependencyMatches(source) {
  assertion(typeof source === 'string', 'Package.swift source must be a string')
  // Keep this deliberately narrow: a commented-out or second Skip dependency must not be
  // mistaken for the active dependency that Dependabot is updating.
  const expression =
    /^\s*\.package\s*\(\s*url\s*:\s*"(https:\/\/source\.skip\.tools\/skip\.git)"\s*,\s*exact\s*:\s*"([^"]+)"\s*\)\s*,?\s*$/gmu
  return [...source.matchAll(expression)].filter(match => !/^[ \t]*\/\//u.test(match[0]))
}

export function parseSkipDependency(source) {
  const matches = skipDependencyMatches(source)
  assertion(
    matches.length === 1,
    'Package.swift must contain exactly one active Skip exact dependency',
  )
  const match = matches[0]
  assertion(
    match[1] === SKIP_URL && SEMVER.test(match[2]),
    'Skip dependency must use an exact stable semver',
  )
  return {
    version: match[2],
    start: match.index,
    end: match.index + match[0].length,
    text: match[0],
  }
}

export function validateSkipManifestDelta(trustedSource, candidateSource) {
  const trusted = parseSkipDependency(trustedSource)
  const candidate = parseSkipDependency(candidateSource)
  assertion(
    trusted.version !== candidate.version,
    'Dependabot Swift repair must update the Skip version',
  )
  const trustedCanonical = trustedSource.replace(
    `exact: "${trusted.version}"`,
    `exact: "${candidate.version}"`,
  )
  assertion(
    trustedCanonical === candidateSource,
    'Package.swift may change only the exact Skip version',
  )
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

export function validateSkipResolvedDelta(
  trustedSource,
  candidateSource,
  expectedVersion,
  { requireVersionChange = true } = {},
) {
  const trusted = parseResolvedDocument(trustedSource)
  const candidate = parseResolvedDocument(candidateSource)
  validateResolvedPinDelta(trusted, candidate, { requiredIdentity: 'skip' })
  const trustedPin = trusted.pins.find(pin => pin.identity === 'skip')
  const candidatePin = candidate.pins.find(pin => pin.identity === 'skip')
  assertion(trustedPin && candidatePin, 'Package.resolved must contain the Skip pin')
  assertion(
    candidatePin.location === SKIP_URL,
    'Package.resolved must retain the trusted Skip source',
  )
  assertion(
    candidatePin.state.version === expectedVersion,
    'Skip lock version must match Package.swift',
  )
  if (requireVersionChange)
    assertion(
      trustedPin.state.version !== candidatePin.state.version,
      'Skip lock version must change',
    )
  assertion(
    trustedPin.state.revision !== candidatePin.state.revision,
    'Skip lock revision must change',
  )
  assertion(
    REVISION.test(candidatePin.state.revision),
    'Skip lock revision must be a full commit SHA',
  )
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
  assertion(
    versionMatches.length === 1 && SEMVER.test(versionMatches[0][1]),
    'materializer must have one stable SKIP_VERSION',
  )
  assertion(checksumMatches.length === 1, 'materializer must have one SKIP_MACOS_ZIP_SHA256')
  return { version: versionMatches[0][1], checksum: checksumMatches[0][1] }
}

export async function validateInputFiles({
  root = process.cwd(),
  metadata,
  changedPaths,
  manifest,
  resolved,
  candidateMaterializer,
}) {
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
    assertion(
      manifestSource.trusted === manifestSource.candidate,
      'Package.swift must remain unchanged for a lock-only repair',
    )
  const resolvedDelta = validateSkipResolvedDelta(
    resolvedSource.trusted,
    resolvedSource.candidate,
    manifestDelta.candidateVersion,
    { requireVersionChange: hasManifest },
  )
  if (paths.includes(SWIFT_ANDROID_MATERIALIZER_PATH)) {
    assertion(candidateMaterializer, 'changed materializer source is required for path validation')
    const materializer = validateMaterializerSource(candidateMaterializer)
    assertion(
      materializer.version === manifestDelta.candidateVersion,
      'changed materializer version must match Package.swift',
    )
  }
  return { ...pr, ...manifestDelta, ...resolvedDelta }
}
