import { createHash } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { parseUniqueSwiftBinaryTargetChecksum } from 'vouchington-tooling/swift-source-offset'
import {
  SWIFT_ANDROID_MATERIALIZER_PATH,
  SWIFT_ANDROID_MANIFEST_PATH,
  SWIFT_ANDROID_RESOLVED_PATH,
  validateInputFiles,
  validateMaterializerSource,
  validateProvenance,
} from './validate-dependabot-swift-repair.mjs'
export const SKIP_BINARY_TARGET = 'skip'
export const SKIP_RELEASE_ARCHIVE = version =>
  `https://github.com/skiptools/skip/releases/download/${version}/skip-macos.zip`
export const SKIP_UPSTREAM_MANIFEST = revision =>
  `https://raw.githubusercontent.com/skiptools/skip/${revision}/Package.swift`
function assertion(condition, message) {
  if (!condition) throw new Error(message)
}
function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex')
}
async function responseBytes(response, url) {
  assertion(response?.ok !== false, `failed to download ${url}`)
  if (typeof response.arrayBuffer === 'function')
    return new Uint8Array(await response.arrayBuffer())
  if (typeof response.text === 'function') return new TextEncoder().encode(await response.text())
  throw new Error(`download response for ${url} has no body reader`)
}
async function fetchBytes(url, fetchImpl = globalThis.fetch) {
  assertion(typeof fetchImpl === 'function', 'a fetch implementation is required')
  const response = await fetchImpl(url, { headers: { accept: 'application/octet-stream' } })
  return responseBytes(response, url)
}
export async function resolveSkipRelease({
  version,
  revision,
  fetchImpl = globalThis.fetch,
  upstreamManifest,
}) {
  assertion(/^[a-f0-9]{40}$/u.test(revision), 'Skip release requires the exact resolved revision')
  const archiveUrl = SKIP_RELEASE_ARCHIVE(version)
  const expectedUrl = archiveUrl
  const manifestBytes =
    upstreamManifest === undefined
      ? await fetchBytes(SKIP_UPSTREAM_MANIFEST(revision), fetchImpl)
      : new TextEncoder().encode(upstreamManifest)
  const source = new TextDecoder().decode(manifestBytes)
  const checksum = parseUniqueSwiftBinaryTargetChecksum(source, SKIP_BINARY_TARGET, expectedUrl)
  assertion(
    checksum,
    'upstream Skip Package.swift must contain one active GitHub binary target with a SHA-256 checksum',
  )
  return { version, archiveUrl, checksum, source }
}
export async function hashSkipArchive({ archiveUrl, fetchImpl = globalThis.fetch, archiveBytes }) {
  const bytes = archiveBytes === undefined ? await fetchBytes(archiveUrl, fetchImpl) : archiveBytes
  const archiveSha256 = sha256(bytes)
  return { archiveSha256, bytes }
}
export function materializeSkipSdkSource(trustedSource, version, checksum) {
  const trusted = validateMaterializerSource(trustedSource)
  if (trusted.version === version && trusted.checksum === checksum) return trustedSource
  const versionExpression = /^SKIP_VERSION="[^"]+"$/mu
  const checksumExpression = /^SKIP_MACOS_ZIP_SHA256="[a-f0-9]{64}"$/mu
  const versionSource = trustedSource.replace(versionExpression, `SKIP_VERSION="${version}"`)
  const result = versionSource.replace(checksumExpression, `SKIP_MACOS_ZIP_SHA256="${checksum}"`)
  assertion(result !== trustedSource, 'repair did not change the trusted materializer')
  validateMaterializerSource(result)
  return result
}
export function createSwiftAndroidProvenance({
  repository,
  pullRequestNumber,
  runId,
  mode = 'swift-android',
  baseSha,
  headSha,
  skipVersion,
  skipRevision,
  archiveUrl,
  archiveSha256,
  archiveChecksum,
  manifestSha256,
  resolvedSha256,
  materializerSha256,
}) {
  const provenance = {
    version: 1,
    repository,
    pullRequest: pullRequestNumber,
    workflowRun: runId,
    mode,
    baseSha,
    headSha,
    skipVersion,
    skipRevision,
    archiveUrl,
    archiveSha256,
    archiveChecksum,
    manifestSha256,
    resolvedSha256,
    materializerSha256,
  }
  return validateProvenance(provenance)
}
export async function prepareSwiftAndroidRepair({
  metadata,
  changedPaths,
  trustedManifest,
  candidateManifest,
  trustedResolved,
  candidateResolved,
  trustedMaterializer,
  candidateMaterializer,
  fetchImpl = globalThis.fetch,
  upstreamManifest,
  archiveBytes,
}) {
  const validation = await validateInputFiles({
    metadata,
    changedPaths,
    manifest: { trusted: trustedManifest, candidate: candidateManifest },
    resolved: { trusted: trustedResolved, candidate: candidateResolved },
    candidateMaterializer,
  })
  const release = await resolveSkipRelease({
    version: validation.candidateVersion,
    revision: validation.candidateRevision,
    fetchImpl,
    upstreamManifest,
  })
  const archive = await hashSkipArchive({
    archiveUrl: release.archiveUrl,
    fetchImpl,
    archiveBytes,
  })
  assertion(
    archive.archiveSha256 === release.checksum,
    'downloaded Skip archive hash does not match upstream Package.swift checksum',
  )
  const materializer = materializeSkipSdkSource(
    trustedMaterializer,
    release.version,
    release.checksum,
  )
  if (candidateMaterializer !== undefined)
    assertion(
      candidateMaterializer === materializer,
      'candidate materializer must exactly match the trusted repair output',
    )
  const skipPin = validation.candidate.pins.find(pin => pin.identity === 'skip')
  const provenance = createSwiftAndroidProvenance({
    repository: validation.repository,
    pullRequestNumber: validation.pullRequestNumber,
    runId: validation.runId,
    mode: 'swift-android',
    baseSha: validation.baseSha,
    headSha: validation.headSha,
    skipVersion: release.version,
    skipRevision: skipPin.state.revision,
    archiveUrl: release.archiveUrl,
    archiveSha256: archive.archiveSha256,
    archiveChecksum: release.checksum,
    manifestSha256: sha256(new TextEncoder().encode(candidateManifest)),
    resolvedSha256: sha256(
      new TextEncoder().encode(
        typeof candidateResolved === 'string'
          ? candidateResolved
          : JSON.stringify(candidateResolved),
      ),
    ),
    materializerSha256: sha256(new TextEncoder().encode(materializer)),
  })
  return { materializer, provenance, validation, upstreamManifest: release.source }
}
export async function prepareSwiftAndroidRepairFromFiles({
  root = process.cwd(),
  metadata,
  changedPaths,
  trustedRoot = root,
  candidateRoot = root,
  fetchImpl = globalThis.fetch,
  upstreamManifest,
  archiveBytes,
}) {
  const read = async (base, path) => readFile(resolve(base, path), 'utf8')
  const paths = changedPaths ?? []
  return prepareSwiftAndroidRepair({
    metadata,
    changedPaths,
    trustedManifest: await read(trustedRoot, SWIFT_ANDROID_MANIFEST_PATH),
    candidateManifest: await read(candidateRoot, SWIFT_ANDROID_MANIFEST_PATH),
    trustedResolved: await read(trustedRoot, SWIFT_ANDROID_RESOLVED_PATH),
    candidateResolved: await read(candidateRoot, SWIFT_ANDROID_RESOLVED_PATH),
    trustedMaterializer: await read(trustedRoot, SWIFT_ANDROID_MATERIALIZER_PATH),
    candidateMaterializer: paths.includes(SWIFT_ANDROID_MATERIALIZER_PATH)
      ? await read(candidateRoot, SWIFT_ANDROID_MATERIALIZER_PATH)
      : undefined,
    fetchImpl,
    upstreamManifest,
    archiveBytes,
  })
}
import { writeSwiftAndroidRepair as writeRepair } from './repair-swift-write.mjs'
export async function writeSwiftAndroidRepair(input) {
  return writeRepair({ ...input, prepare: prepareSwiftAndroidRepair })
}
import { main } from './repair-swift-cli.mjs'

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2), { prepareSwiftAndroidRepairFromFiles }).catch(error => {
    process.stderr.write(`::error::${error.message}\n`)
    process.exitCode = 1
  })
}
