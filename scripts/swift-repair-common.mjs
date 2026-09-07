export const SWIFT_ANDROID_MANIFEST_PATH = 'swift-clients/apps/android/Package.swift'
export const SWIFT_ANDROID_RESOLVED_PATH = 'swift-clients/apps/android/Package.resolved'
export const SWIFT_ANDROID_MATERIALIZER_PATH =
  'swift-clients/apps/android/tooling/materialize-skip-sdk.sh'
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
export const SHA256 = /^[a-f0-9]{64}$/u
export const SHA1 = /^[a-f0-9]{40}$/u
export const SEMVER =
  /^(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/u
export const REVISION = /^[a-f0-9]{40}$/u
export const SKIP_URL = 'https://source.skip.tools/skip.git'

export function assertion(condition, message) {
  if (!condition) throw new Error(message)
}
export function uniqueValues(values, label) {
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
export function parseRawGitDiff(raw) {
  assertion(typeof raw === 'string' || Buffer.isBuffer(raw), 'raw git diff is required')
  const source = Buffer.isBuffer(raw) ? raw.toString('utf8') : raw
  const fields = source.split('\0'),
    entries = []
  for (let index = 0; index < fields.length;) {
    const header = fields[index++]
    if (header === '') continue
    assertion(header.startsWith(':'), 'raw git diff contains an invalid record')
    const parts = header.slice(1).split(' ')
    assertion(parts.length === 5, 'raw git diff record is malformed')
    const [oldMode, newMode, oldSha, newSha, status] = parts
    assertion(
      oldMode === '100644' && newMode === '100644',
      'Swift repair accepts regular files only',
    )
    assertion(
      /^[0-9a-f]{7,64}$/u.test(oldSha) && /^[0-9a-f]{7,64}$/u.test(newSha),
      'raw git diff contains an invalid object id',
    )
    assertion(status === 'M', `raw git diff status ${status} is not an in-place modification`)
    const path = fields[index++]
    assertion(
      path && !path.includes('\0') && !path.startsWith('/') && !path.includes('..'),
      'raw git diff contains an invalid path',
    )
    entries.push({ oldMode, newMode, oldSha, newSha, status, path })
  }
  assertion(entries.length > 0, 'raw git diff is empty')
  return entries
}
export function validateDependabotPullRequest(metadata) {
  assertion(metadata && typeof metadata === 'object', 'pull-request metadata is required')
  const pullRequest = metadata.pull_request ?? metadata,
    repository = metadata.repository ?? {}
  assertion(
    pullRequest.user?.login === 'dependabot[bot]',
    'pull request is not authored by Dependabot',
  )
  assertion(
    pullRequest.base?.ref ===
      (repository.default_branch ?? pullRequest.base?.repo?.default_branch ?? 'main'),
    'pull request must target the repository default branch',
  )
  const repositoryName = repository.full_name ?? pullRequest.base?.repo?.full_name
  assertion(
    repositoryName && pullRequest.head?.repo?.full_name === repositoryName,
    'pull request head must be in this repository',
  )
  assertion(
    (pullRequest.head?.ref ?? '').startsWith('dependabot/'),
    'pull request head must be a Dependabot branch',
  )
  assertion(
    SHA1.test(pullRequest.base?.sha ?? ''),
    'pull request base SHA must be a full commit SHA',
  )
  assertion(
    SHA1.test(pullRequest.head?.sha ?? ''),
    'pull request head SHA must be a full commit SHA',
  )
  return {
    baseSha: pullRequest.base.sha,
    headSha: pullRequest.head.sha,
    headRef: pullRequest.head.ref,
    repository: repositoryName,
    pullRequestNumber: pullRequest.number,
    runId: metadata.run_id ?? metadata.runId,
  }
}
