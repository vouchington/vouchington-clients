import {
  DEPENDABOT_LOGIN,
  DEPENDABOT_REF_PREFIX,
  fail,
  requireExact,
  requirePositiveInteger,
  requireRecord,
  requireRepository,
  requireSha,
} from './validate-dependabot-dotnet-repair-shared.mjs'
import { validateDotnetRepairRawDiff } from './validate-dependabot-dotnet-repair-raw-diff.mjs'

export function validateDotnetRepairPullRequest(
  pullRequest,
  rawDiff,
  defaultBranch,
  repository,
  headRef,
  baseSha,
  headSha,
) {
  const expectedRepository = requireRepository(repository, 'repository')
  const expectedBaseSha = requireSha(baseSha, 'base SHA')
  const expectedHeadSha = requireSha(headSha, 'head SHA')
  if (typeof defaultBranch !== 'string' || defaultBranch.length === 0) {
    fail('default branch must be nonempty')
  }
  if (typeof headRef !== 'string' || !headRef.startsWith(DEPENDABOT_REF_PREFIX)) {
    fail('head ref must be a Dependabot branch')
  }
  const live = requireRecord(pullRequest, 'pull request')
  const user = requireRecord(live.user, 'pull request user')
  const base = requireRecord(live.base, 'pull request base')
  const head = requireRecord(live.head, 'pull request head')
  const baseRepository = requireRecord(base.repo, 'pull request base repository')
  const headRepository = requireRecord(head.repo, 'pull request head repository')
  const number = requirePositiveInteger(live.number, 'pull request number')
  const changedFiles = requirePositiveInteger(live.changed_files, 'pull request changed_files')
  requireExact(live.state, 'open', 'pull request state')
  requireExact(live.draft, false, 'pull request draft status')
  requireExact(user.login, DEPENDABOT_LOGIN, 'pull request author')
  requireExact(base.ref, defaultBranch, 'pull request base branch')
  requireExact(base.sha, expectedBaseSha, 'pull request base SHA')
  requireExact(baseRepository.full_name, expectedRepository, 'pull request base repository')
  requireExact(head.ref, headRef, 'pull request head branch')
  requireExact(head.sha, expectedHeadSha, 'pull request head SHA')
  requireExact(headRepository.full_name, expectedRepository, 'pull request head repository')
  const changedPaths = validateDotnetRepairRawDiff(rawDiff)
  if (changedFiles !== changedPaths.length) {
    fail('pull request changed_files did not match the raw diff')
  }
  return Object.freeze({ number, baseSha: expectedBaseSha, headSha: expectedHeadSha, changedPaths })
}
