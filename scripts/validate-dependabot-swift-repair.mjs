import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

export * from './swift-repair-common.mjs'
export {
  parseResolvedDocument,
  parseSkipDependency,
  validateInputFiles,
  validateMaterializerSource,
  validateSkipManifestDelta,
  validateSkipResolvedDelta,
} from './swift-repair-input.mjs'
import { validateInputFiles } from './swift-repair-input.mjs'
import {
  assertion,
  parseRawGitDiff,
  SHA1,
  validatePullRequestPaths,
  validatePublishedArtifactPaths,
} from './swift-repair-common.mjs'
import {
  validateProvenance,
  validateSwiftAndroidRepairProvenance,
} from './swift-repair-provenance.mjs'

export const validatePublishedPaths = validatePublishedArtifactPaths
export { validateProvenance, validateSwiftAndroidRepairProvenance }

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
  assertion(
    typeof defaultBranch === 'string' && defaultBranch.length > 0,
    'default branch must be nonempty',
  )
  assertion(
    typeof repository === 'string' && /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/u.test(repository),
    'repository must be an owner/name repository',
  )
  assertion(SHA1.test(baseSha), 'base SHA must be a lowercase 40-character Git SHA')
  assertion(SHA1.test(headSha), 'head SHA must be a lowercase 40-character Git SHA')
  assertion(
    typeof headRef === 'string' && headRef.startsWith('dependabot/'),
    'head ref must be a Dependabot branch',
  )
  const live = pullRequest
  assertion(live && typeof live === 'object', 'pull request must be an object')
  assertion(live.user?.login === 'dependabot[bot]', 'pull request author must be Dependabot')
  assertion(live.state === 'open', 'pull request must be open')
  assertion(live.draft === false, 'pull request must not be a draft')
  assertion(
    live.base?.ref === defaultBranch,
    'pull request base branch did not match trusted context',
  )
  assertion(live.base?.sha === baseSha, 'pull request base SHA did not match trusted context')
  assertion(
    live.base?.repo?.full_name === repository,
    'pull request base repository did not match trusted context',
  )
  assertion(live.head?.ref === headRef, 'pull request head ref did not match trusted context')
  assertion(live.head?.sha === headSha, 'pull request head SHA did not match trusted context')
  assertion(
    live.head?.repo?.full_name === repository,
    'pull request head repository did not match trusted context',
  )
  const changedPaths = validateSwiftAndroidRepairRawDiff(rawDiff)
  assertion(
    live.changed_files === changedPaths.length,
    'pull request changed_files did not match raw diff',
  )
  return Object.freeze({ number: live.number, baseSha, headSha, changedPaths })
}

import { main } from './swift-repair-cli.mjs'

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2), {
    validateInputFiles,
    validateProvenance,
    validateSwiftAndroidRepairProvenance,
    validateSwiftAndroidRepairPullRequest,
  }).catch(error => {
    process.stderr.write(`::error::${error.message}\n`)
    process.exitCode = 1
  })
}
