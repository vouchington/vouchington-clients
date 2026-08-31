import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'

export const NUGET_LOCK_PATHS = Object.freeze([
  'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
  'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
  'dotnet-clients/tests/Voucha.Client.App.Tests/packages.lock.json',
  'dotnet-clients/tests/Voucha.Client.Core.Tests/packages.lock.json',
])
const SORTED_NUGET_LOCK_PATHS = Object.freeze([...NUGET_LOCK_PATHS].toSorted())

const DEPENDABOT_LOGIN = 'dependabot[bot]'
const DEPENDABOT_REF_PREFIX = 'dependabot/'
const NUGET_MANIFEST_PATH = 'dotnet-clients/Directory.Packages.props'
const SHA_PATTERN = /^[0-9a-f]{40}$/u
const SHA256_PATTERN = /^[0-9a-f]{64}$/u
const POSITIVE_INTEGER_PATTERN = /^[1-9][0-9]*$/u
const REPOSITORY_PATTERN = /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/u
const RAW_MODIFICATION_PATTERN = /^:100644 100644 [0-9a-f]{7,64} [0-9a-f]{7,64} M$/u

function fail(message) {
  throw new Error(message)
}

function isRecord(value) {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function requireRecord(value, label) {
  if (!isRecord(value)) fail(`${label} must be an object`)
  return value
}

function requireExactKeys(value, keys, label) {
  const actual = Object.keys(requireRecord(value, label)).toSorted()
  const expected = [...keys].toSorted()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    fail(`${label} has unexpected fields`)
  }
}

function requireSha(value, label) {
  if (typeof value !== 'string' || !SHA_PATTERN.test(value)) fail(`${label} must be a lowercase 40-character Git SHA`)
  return value
}

function requireSha256(value, label) {
  if (typeof value !== 'string' || !SHA256_PATTERN.test(value)) fail(`${label} must be a lowercase SHA-256 digest`)
  return value
}

function requirePositiveInteger(value, label) {
  const source = typeof value === 'number' ? String(value) : value
  if (typeof source !== 'string' || !POSITIVE_INTEGER_PATTERN.test(source)) {
    fail(`${label} must be a positive integer`)
  }
  const parsed = Number(source)
  if (!Number.isSafeInteger(parsed)) fail(`${label} must be a safe integer`)
  return parsed
}

function requireRepository(value, label) {
  if (typeof value !== 'string' || !REPOSITORY_PATTERN.test(value)) fail(`${label} must be an owner/name repository`)
  return value
}

function requireExact(value, expected, label) {
  if (value !== expected) fail(`${label} did not match the trusted context`)
}

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
  if (typeof defaultBranch !== 'string' || defaultBranch.length === 0) fail('default branch must be nonempty')
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
  if (changedFiles !== changedPaths.length) fail('pull request changed_files did not match the raw diff')

  return Object.freeze({ number, baseSha: expectedBaseSha, headSha: expectedHeadSha, changedPaths })
}

export function validateDotnetRepairRawDiff(rawDiff) {
  if (typeof rawDiff !== 'string' && !Buffer.isBuffer(rawDiff)) fail('raw diff must be a string or buffer')
  const source = Buffer.isBuffer(rawDiff) ? rawDiff.toString('utf8') : rawDiff
  if (!source.endsWith('\0')) fail('raw diff must be NUL-delimited')
  const fields = source.slice(0, -1).split('\0')
  if (fields.length === 0 || fields.length % 2 !== 0) fail('raw diff must contain complete modification records')
  const allowedPaths = new Set([NUGET_MANIFEST_PATH, ...NUGET_LOCK_PATHS])
  const changedPaths = []
  for (let index = 0; index < fields.length; index += 2) {
    if (!RAW_MODIFICATION_PATTERN.test(fields[index])) {
      fail('Dependabot raw diff must contain only regular-file modifications')
    }
    const path = fields[index + 1]
    if (!allowedPaths.has(path) || changedPaths.includes(path)) {
      fail('Dependabot may change only its central manifest and existing NuGet locks')
    }
    changedPaths.push(path)
  }
  if (!changedPaths.includes(NUGET_MANIFEST_PATH)) {
    fail('Dependabot must change dotnet-clients/Directory.Packages.props')
  }
  return changedPaths
}

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
  requireExactKeys(provenance, ['version', 'repository', 'pullRequest', 'workflowRun', 'baseSha', 'headSha', 'manifestSha256', 'locks'], 'repair provenance')
  requireExact(provenance.version, 1, 'repair provenance version')
  requireExact(provenance.repository, expectedRepository, 'repair provenance repository')
  requireExact(provenance.pullRequest, expectedPullRequest, 'repair provenance pull request')
  requireExact(provenance.workflowRun, expectedWorkflowRun, 'repair provenance workflow run')
  requireExact(provenance.baseSha, expectedBaseSha, 'repair provenance base SHA')
  requireExact(provenance.headSha, expectedHeadSha, 'repair provenance head SHA')
  requireSha256(provenance.manifestSha256, 'repair provenance manifest hash')
  if (!Array.isArray(provenance.locks) || provenance.locks.length !== NUGET_LOCK_PATHS.length) {
    fail('repair provenance must declare exactly seven NuGet locks')
  }
  provenance.locks.forEach((lock, index) => {
    requireExactKeys(lock, ['path', 'sha256'], `repair provenance lock ${index + 1}`)
    requireExact(lock.path, NUGET_LOCK_PATHS[index], `repair provenance lock ${index + 1} path`)
    requireSha256(lock.sha256, `repair provenance lock ${index + 1} hash`)
  })
  return provenance.locks.map(lock => ({ ...lock }))
}

export function validateDotnetRepairPublishedPaths(rawPaths) {
  if (typeof rawPaths !== 'string' && !Buffer.isBuffer(rawPaths)) fail('published paths must be a string or buffer')
  const source = Buffer.isBuffer(rawPaths) ? rawPaths.toString('utf8') : rawPaths
  if (!source.endsWith('\0')) fail('published paths must be NUL-delimited')
  const paths = source.slice(0, -1).split('\0')
  if (paths.length === 0 || new Set(paths).size !== paths.length) {
    fail('repair publish must change a nonempty unique NuGet lock subset')
  }
  const allowed = new Set(SORTED_NUGET_LOCK_PATHS)
  if (paths.some(path => !allowed.has(path))) {
    fail('repair publish may change only the seven committed NuGet lock files')
  }
  return [...paths]
}

async function runCli(args) {
  const [mode, ...modeArgs] = args
  switch (mode) {
    case 'pull-request': {
      const [pullRequestPath, rawDiffPath, defaultBranch, repository, headRef, baseSha, headSha] = modeArgs
      if (modeArgs.length !== 7) fail('Usage: validate-dependabot-dotnet-repair.mjs pull-request <live-pr.json> <raw-diff> <default-branch> <repository> <head-ref> <base-sha> <head-sha>')
      const [pullRequest, rawDiff] = await Promise.all([readFile(pullRequestPath, 'utf8'), readFile(rawDiffPath)])
      validateDotnetRepairPullRequest(JSON.parse(pullRequest), rawDiff, defaultBranch, repository, headRef, baseSha, headSha)
      return
    }
    case 'provenance': {
      const [provenancePath, repository, pullRequest, workflowRun, baseSha, headSha] = modeArgs
      if (modeArgs.length !== 6) fail('Usage: validate-dependabot-dotnet-repair.mjs provenance <provenance.json> <repository> <pr> <run> <base-sha> <head-sha>')
      validateDotnetRepairProvenance(JSON.parse(await readFile(provenancePath, 'utf8')), repository, pullRequest, workflowRun, baseSha, headSha)
      return
    }
    case 'published-paths': {
      const [pathsPath] = modeArgs
      if (modeArgs.length !== 1) fail('Usage: validate-dependabot-dotnet-repair.mjs published-paths <NUL-delimited-path-file>')
      validateDotnetRepairPublishedPaths(await readFile(pathsPath))
      return
    }
    default:
      fail('Usage: validate-dependabot-dotnet-repair.mjs <pull-request|provenance|published-paths> ...')
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  runCli(process.argv.slice(2)).catch(error => {
    process.stderr.write(`${error.message}\n`)
    process.exitCode = 1
  })
}
