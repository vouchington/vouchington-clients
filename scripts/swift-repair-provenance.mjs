import { assertion, REVISION, SEMVER, SHA1, SHA256 } from './swift-repair-common.mjs'

export function validateProvenance(provenance) {
  assertion(provenance && typeof provenance === 'object', 'provenance must be an object')
  const expectedKeys = [
    'archiveChecksum',
    'archiveSha256',
    'archiveUrl',
    'baseSha',
    'headSha',
    'manifestSha256',
    'materializerSha256',
    'mode',
    'pullRequest',
    'repository',
    'resolvedSha256',
    'skipRevision',
    'skipVersion',
    'version',
    'workflowRun',
  ]
  assertion(
    Object.keys(provenance).toSorted().join('\0') === expectedKeys.toSorted().join('\0'),
    'Swift Android provenance fields do not match the supported schema',
  )
  assertion(provenance.version === 1, 'unsupported Swift Android provenance schema')
  assertion(
    typeof provenance.repository === 'string' && provenance.repository.length > 0,
    'provenance repository is required',
  )
  assertion(
    typeof provenance.pullRequest === 'number' &&
      Number.isSafeInteger(provenance.pullRequest) &&
      provenance.pullRequest > 0,
    'provenance pull request number is required',
  )
  assertion(
    typeof provenance.workflowRun === 'number' &&
      Number.isSafeInteger(provenance.workflowRun) &&
      provenance.workflowRun > 0,
    'provenance workflow run is required',
  )
  assertion(provenance.mode === 'swift-android', 'provenance mode is invalid')
  assertion(SHA1.test(provenance.baseSha), 'provenance baseSha must be a full commit SHA')
  assertion(SHA1.test(provenance.headSha), 'provenance headSha must be a full commit SHA')
  assertion(SEMVER.test(provenance.skipVersion), 'provenance Skip version is invalid')
  assertion(REVISION.test(provenance.skipRevision), 'provenance Skip revision is invalid')
  assertion(
    typeof provenance.archiveUrl === 'string' &&
      provenance.archiveUrl ===
        `https://github.com/skiptools/skip/releases/download/${provenance.skipVersion}/skip-macos.zip`,
    'provenance archive URL is not the resolved Skip release',
  )
  assertion(SHA256.test(provenance.archiveSha256), 'provenance archive SHA-256 is invalid')
  assertion(SHA256.test(provenance.archiveChecksum), 'provenance archive checksum is invalid')
  assertion(
    provenance.archiveSha256 === provenance.archiveChecksum,
    'provenance archive hash does not match the Skip checksum',
  )
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
  assertion(
    Number.isSafeInteger(Number(pullRequest)) && Number(pullRequest) > 0,
    'pull request number must be a positive integer',
  )
  assertion(
    Number.isSafeInteger(Number(workflowRun)) && Number(workflowRun) > 0,
    'workflow run must be a positive integer',
  )
  assertion(
    SHA1.test(baseSha) && SHA1.test(headSha),
    'provenance context requires full commit SHAs',
  )
  assertion(value.repository === repository, 'provenance repository did not match trusted context')
  assertion(
    value.pullRequest === Number(pullRequest),
    'provenance pull request did not match trusted context',
  )
  assertion(
    value.workflowRun === Number(workflowRun),
    'provenance workflow run did not match trusted context',
  )
  assertion(value.baseSha === baseSha, 'provenance base SHA did not match trusted context')
  assertion(value.headSha === headSha, 'provenance head SHA did not match trusted context')
  return value
}
