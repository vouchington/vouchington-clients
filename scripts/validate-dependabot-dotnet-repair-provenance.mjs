import {
  fail,
  NUGET_LOCK_PATHS,
  requireExact,
  requireExactKeys,
  requirePositiveInteger,
  requireRepository,
  requireSha,
  requireSha256,
} from './validate-dependabot-dotnet-repair-shared.mjs'

const SORTED_NUGET_LOCK_PATHS = Object.freeze([...NUGET_LOCK_PATHS].toSorted())

export function validateDotnetRepairProvenance(
  provenance,
  repository,
  pullRequest,
  workflowRun,
  baseSha,
  headSha,
) {
  const expectedRepository = requireRepository(repository, 'repository')
  const expectedPullRequest = requirePositiveInteger(pullRequest, 'pull request number')
  const expectedWorkflowRun = requirePositiveInteger(workflowRun, 'workflow run')
  const expectedBaseSha = requireSha(baseSha, 'base SHA')
  const expectedHeadSha = requireSha(headSha, 'head SHA')
  requireExactKeys(
    provenance,
    [
      'version',
      'repository',
      'pullRequest',
      'workflowRun',
      'baseSha',
      'headSha',
      'manifestSha256',
      'locks',
    ],
    'repair provenance',
  )
  requireExact(provenance.version, 1, 'repair provenance version')
  requireExact(provenance.repository, expectedRepository, 'repair provenance repository')
  requireExact(provenance.pullRequest, expectedPullRequest, 'repair provenance pull request')
  requireExact(provenance.workflowRun, expectedWorkflowRun, 'repair provenance workflow run')
  requireExact(provenance.baseSha, expectedBaseSha, 'repair provenance base SHA')
  requireExact(provenance.headSha, expectedHeadSha, 'repair provenance head SHA')
  requireSha256(provenance.manifestSha256, 'repair provenance manifest hash')
  if (!Array.isArray(provenance.locks) || provenance.locks.length !== NUGET_LOCK_PATHS.length) {
    fail('repair provenance must declare exactly eight NuGet locks')
  }
  provenance.locks.forEach((lock, index) => {
    requireExactKeys(lock, ['path', 'sha256'], `repair provenance lock ${index + 1}`)
    requireExact(lock.path, NUGET_LOCK_PATHS[index], `repair provenance lock ${index + 1} path`)
    requireSha256(lock.sha256, `repair provenance lock ${index + 1} hash`)
  })
  return provenance.locks.map(lock => ({ ...lock }))
}

export function validateDotnetRepairPublishedPaths(rawPaths) {
  if (typeof rawPaths !== 'string' && !Buffer.isBuffer(rawPaths)) {
    fail('published paths must be a string or buffer')
  }
  const source = Buffer.isBuffer(rawPaths) ? rawPaths.toString('utf8') : rawPaths
  if (!source.endsWith('\0')) fail('published paths must be NUL-delimited')
  const paths = source.slice(0, -1).split('\0')
  if (paths.length === 0 || new Set(paths).size !== paths.length) {
    fail('repair publish must change a nonempty unique NuGet lock subset')
  }
  const allowed = new Set(SORTED_NUGET_LOCK_PATHS)
  if (paths.some(path => !allowed.has(path))) {
    fail('repair publish may change only the eight committed NuGet lock files')
  }
  return [...paths]
}
