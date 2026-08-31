import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { describe, it } from 'node:test'

import {
  parseRawGitDiff,
  validateDependabotPullRequest,
  validatePublishedArtifactPaths,
  validatePublishedCommitPaths,
  validatePullRequestPaths,
  validateProvenance,
  validateSkipManifestDelta,
  validateSkipResolvedDelta,
  validateSwiftAndroidRepairProvenance,
  validateSwiftAndroidRepairPullRequest,
} from '../scripts/validate-dependabot-swift-repair.mjs'
import {
  createSwiftAndroidProvenance,
  hashSkipArchive,
  materializeSkipSdkSource,
  resolveSkipRelease,
} from '../scripts/repair-dependabot-swift-android.mjs'

const manifest = await readFile(new URL('../swift-clients/apps/android/Package.swift', import.meta.url), 'utf8')
const resolved = JSON.parse(await readFile(new URL('../swift-clients/apps/android/Package.resolved', import.meta.url), 'utf8'))
const materializer = await readFile(new URL('../swift-clients/apps/android/tooling/materialize-skip-sdk.sh', import.meta.url), 'utf8')
const sha = 'a'.repeat(40)

function candidateResolved(version = '1.9.8') {
  const candidate = structuredClone(resolved)
  candidate.originHash = 'b'.repeat(64)
  const skip = candidate.pins.find(pin => pin.identity === 'skip')
  skip.state.version = version
  skip.state.revision = sha
  return candidate
}

describe('Swift Android Dependabot repair validation', () => {
  it('parses NUL-delimited raw git records and rejects symlinks, additions, and renames', () => {
    const raw = `:100644 100644 ${sha} ${'b'.repeat(40)} M\0swift-clients/apps/android/Package.swift\0`
    assert.deepEqual(parseRawGitDiff(raw)[0].path, 'swift-clients/apps/android/Package.swift')
    assert.throws(() => parseRawGitDiff(`:120000 100644 ${sha} ${'b'.repeat(40)} M\0x\0`))
    assert.throws(() => parseRawGitDiff(`:100644 100644 ${sha} ${'b'.repeat(40)} A\0x\0`))
  })

  it('accepts only the three established Dependabot path sets', () => {
    validatePullRequestPaths(['swift-clients/apps/android/Package.resolved'])
    validatePullRequestPaths([
      'swift-clients/apps/android/Package.swift',
      'swift-clients/apps/android/Package.resolved',
    ])
    assert.throws(() => validatePullRequestPaths(['swift-clients/apps/android/Package.swift']))
    assert.throws(() => validatePublishedCommitPaths([
      'swift-clients/apps/android/tooling/materialize-skip-sdk.sh',
      'dependabot-swift-android-provenance.json',
    ]))
    validatePublishedArtifactPaths([
      'swift-clients/apps/android/tooling/materialize-skip-sdk.sh',
      'dependabot-swift-android-provenance.json',
    ])
  })

  it('requires the manifest delta to contain exactly one Skip version replacement', () => {
    const candidate = manifest.replace('exact: "1.9.7"', 'exact: "1.9.8"')
    assert.deepEqual(validateSkipManifestDelta(manifest, candidate).candidateVersion, '1.9.8')
    assert.throws(() => validateSkipManifestDelta(manifest, `${candidate}\n// injected`))
  })

  it('freezes non-Skip pins while permitting fully specified transitive additions', () => {
    const candidate = candidateResolved()
    assert.equal(validateSkipResolvedDelta(resolved, candidate, '1.9.8').candidateRevision, sha)
    const tampered = structuredClone(candidate)
    const unrelatedPin = tampered.pins.find(pin => pin.identity !== 'skip')
    assert.ok(unrelatedPin, 'fixture requires one non-Skip pin')
    unrelatedPin.state.revision = sha
    assert.throws(() => validateSkipResolvedDelta(resolved, tampered, '1.9.9'))
  })

  it('validates trusted Dependabot identity, repository, ref, and full SHAs', () => {
    const metadata = {
      run_id: 42,
      repository: { full_name: 'vouchington/vouchington-clients', default_branch: 'main' },
      pull_request: {
        number: 7,
        user: { login: 'dependabot[bot]' },
        base: { ref: 'main', sha: sha, repo: { full_name: 'vouchington/vouchington-clients' } },
        head: { ref: 'dependabot/swift/skip', sha: 'b'.repeat(40), repo: { full_name: 'vouchington/vouchington-clients' } },
      },
    }
    assert.equal(validateDependabotPullRequest(metadata).pullRequestNumber, 7)
    assert.throws(() => validateDependabotPullRequest({ ...metadata, pull_request: { ...metadata.pull_request, user: { login: 'evil' } } }))
    const live = { ...metadata.pull_request, state: 'open', draft: false, changed_files: 1 }
    const raw = `:100644 100644 ${sha} ${'b'.repeat(40)} M\0swift-clients/apps/android/Package.resolved\0`
    validateSwiftAndroidRepairPullRequest(
      live,
      raw,
      'main',
      'vouchington/vouchington-clients',
      'dependabot/swift/skip',
      sha,
      'b'.repeat(40),
    )
    assert.throws(() => validateSwiftAndroidRepairPullRequest(
      { ...live, head: { ...live.head, sha: 'c'.repeat(40) } },
      raw,
      'main',
      'vouchington/vouchington-clients',
      'dependabot/swift/skip',
      sha,
      'b'.repeat(40),
    ))
  })

  it('materializes only the version and checksum fields and hashes the downloaded bytes', async () => {
    const source = materializeSkipSdkSource(materializer, '1.9.8', 'c'.repeat(64))
    assert.match(source, /SKIP_VERSION="1\.9\.8"/u)
    assert.match(source, /SKIP_MACOS_ZIP_SHA256="c{64}"/u)
    const archive = await hashSkipArchive({ archiveUrl: 'unused', archiveBytes: new TextEncoder().encode('archive') })
    assert.equal(archive.archiveSha256.length, 64)
  })

  it('reads upstream checksum metadata from the exact resolved revision', async () => {
    const revision = 'b'.repeat(40)
    const checksum = 'c'.repeat(64)
    const upstream = `package.targets += [.binaryTarget(\n  name: "skip",\n  url: "https://github.com/skiptools/skip/releases/download/1.9.8/skip-macos.zip",\n  checksum: "${checksum}"\n)]`
    let requested
    const result = await resolveSkipRelease({
      version: '1.9.8',
      revision,
      fetchImpl: async url => {
        requested = url
        return { ok: true, text: async () => upstream }
      },
    })
    assert.equal(requested, `https://raw.githubusercontent.com/skiptools/skip/${revision}/Package.swift`)
    assert.equal(result.checksum, checksum)
  })

  it('emits provenance with immutable context and source hashes', () => {
    const provenance = createSwiftAndroidProvenance({
      repository: 'vouchington/vouchington-clients',
      pullRequestNumber: 7,
      runId: 42,
      baseSha: sha,
      headSha: 'b'.repeat(40),
      skipVersion: '1.9.8',
      skipRevision: sha,
      archiveUrl: 'https://github.com/skiptools/skip/releases/download/1.9.8/skip-macos.zip',
      archiveSha256: 'c'.repeat(64),
      archiveChecksum: 'c'.repeat(64),
      manifestSha256: 'd'.repeat(64),
      resolvedSha256: 'e'.repeat(64),
      materializerSha256: 'f'.repeat(64),
    })
    assert.equal(provenance.mode, 'swift-android')
    assert.equal(provenance.version, 1)
    assert.equal(provenance.pullRequest, 7)
    assert.equal(provenance.workflowRun, 42)
    validateSwiftAndroidRepairProvenance(
      provenance,
      'vouchington/vouchington-clients',
      '7',
      '42',
      sha,
      'b'.repeat(40),
    )
    assert.throws(() => validateSwiftAndroidRepairProvenance(
      provenance,
      'vouchington/vouchington-clients',
      '7',
      '43',
      sha,
      'b'.repeat(40),
    ))
    assert.throws(() => validateProvenance({ ...provenance, unexpected: true }))
  })
})
