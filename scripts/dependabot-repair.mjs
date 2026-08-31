import { createHash } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { join } from 'node:path'

import { validateNugetUpdate } from 'vouchington-tooling/nuget-central-version'

export const dotnetLockPaths = [
  'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
  'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
  'dotnet-clients/tests/Voucha.Client.App.Tests/packages.lock.json',
  'dotnet-clients/tests/Voucha.Client.Core.Tests/packages.lock.json',
]

export const androidRepairPaths = ['swift-clients/apps/android/tooling/materialize-skip-sdk.sh']
export const candidateDependencyPaths = [
  'dotnet-clients/Directory.Packages.props',
  ...dotnetLockPaths,
  'swift-clients/apps/android/Package.swift',
  'swift-clients/apps/android/Package.resolved',
  'swift-clients/apps/android/tooling/materialize-skip-sdk.sh',
]

const candidateHashPaths = [
  'dotnet-clients/Directory.Packages.props',
  'swift-clients/apps/android/Package.swift',
  'swift-clients/apps/android/Package.resolved',
]

const sha = /^[a-f0-9]{40}$/u

export function validateCandidate(candidate, expected) {
  if (
    candidate?.state !== 'open' ||
    candidate?.user?.login !== 'dependabot[bot]' ||
    candidate?.base?.ref !== expected.defaultBranch ||
    candidate?.base?.sha !== expected.baseSha ||
    candidate?.head?.sha !== expected.headSha ||
    candidate?.head?.ref !== expected.headRef ||
    candidate?.head?.repo?.full_name !== expected.repository ||
    !expected.headRef.startsWith('dependabot/') ||
    !sha.test(expected.baseSha) ||
    !sha.test(expected.headSha)
  ) {
    throw new Error('Live pull request identity does not match the validated Dependabot update')
  }
}

export function validatePublishedArtifact(paths) {
  const allowed = new Set([...dotnetLockPaths, ...androidRepairPaths])
  if (
    paths.length !== allowed.size ||
    new Set(paths).size !== paths.length ||
    !paths.every(path => allowed.has(path))
  ) {
    throw new Error('Repair artifact may contain only the generated native allowlist')
  }
}

export function validatePublishedPaths(paths) {
  const allowed = new Set([...dotnetLockPaths, ...androidRepairPaths])
  if (paths.length === 0 || new Set(paths).size !== paths.length || !paths.every(path => allowed.has(path))) {
    throw new Error('Repair publisher may commit only the generated native allowlist')
  }
}

export function validateCandidatePaths(paths) {
  const allowed = new Set(candidateDependencyPaths)
  const hasDotnetChange = paths.some(path => path.startsWith('dotnet-clients/'))
  const hasAndroidChange = paths.some(path => path.startsWith('swift-clients/apps/android/'))
  if (
    paths.length === 0 ||
    new Set(paths).size !== paths.length ||
    !paths.every(path => allowed.has(path)) ||
    (hasDotnetChange && !paths.includes('dotnet-clients/Directory.Packages.props')) ||
    (hasDotnetChange && hasAndroidChange)
  ) {
    throw new Error('Dependabot candidate may modify only native dependency inputs')
  }
}

export function materializeNugetUpdate(trustedSource, candidateSource, metadataSource) {
  const changed = validateNugetUpdate(trustedSource, candidateSource, metadataSource)
  const updates = new Map(JSON.parse(metadataSource).map(update => [update.dependencyName, update]))
  let materialized = trustedSource
  for (const dependencyName of changed) {
    const update = updates.get(dependencyName)
    const trustedLiteral = `<PackageVersion Include="${dependencyName}" Version="${update.prevVersion}" />`
    const replacement = `<PackageVersion Include="${dependencyName}" Version="${update.newVersion}" />`
    if (!update || materialized.split(trustedLiteral).length !== 2) {
      throw new Error(`Trusted NuGet manifest has no unique literal for ${dependencyName}`)
    }
    materialized = materialized.replace(trustedLiteral, replacement)
  }
  return materialized
}

export async function canonicalHash(root, paths) {
  const aggregate = createHash('sha256')
  for (const path of paths) {
    const digest = createHash('sha256').update(await readFile(join(root, path))).digest('hex')
    aggregate.update(`${path}\0${digest}\n`)
  }
  return aggregate.digest('hex')
}

export function validateProvenance(provenance, expected) {
  const hash = /^[a-f0-9]{64}$/u
  if (
    provenance?.repository !== expected.repository ||
    provenance?.pr !== expected.pr ||
    provenance?.run !== expected.run ||
    provenance?.baseSha !== expected.baseSha ||
    provenance?.headSha !== expected.headSha ||
    !hash.test(provenance?.candidateHash ?? '') ||
    !hash.test(provenance?.locksHash ?? '') ||
    !hash.test(provenance?.androidHash ?? '')
  ) {
    throw new Error('Repair artifact provenance does not match this workflow run')
  }
}

async function main() {
  const [command, ...args] = process.argv.slice(2)
  if (command === 'candidate') {
    const [path, defaultBranch, repository, headRef, baseSha, headSha] = args
    if (!path || !defaultBranch || !repository || !headRef || !baseSha || !headSha) {
      throw new Error('candidate requires live JSON, default branch, repository, ref, and SHAs')
    }
    validateCandidate(JSON.parse(await readFile(path, 'utf8')), {
      baseSha,
      defaultBranch,
      headRef,
      headSha,
      repository,
    })
    return
  }
  if (command === 'artifact-paths') {
    const [path] = args
    if (!path) throw new Error('artifact-paths requires a NUL-delimited path file')
    validatePublishedArtifact((await readFile(path, 'utf8')).split('\0').filter(Boolean))
    return
  }
  if (command === 'candidate-paths') {
    const [path] = args
    if (!path) throw new Error('candidate-paths requires a newline-delimited path file')
    validateCandidatePaths((await readFile(path, 'utf8')).split('\n').filter(Boolean))
    return
  }
  if (command === 'published-paths') {
    const [path] = args
    if (!path) throw new Error('published-paths requires a NUL-delimited path file')
    validatePublishedPaths((await readFile(path, 'utf8')).split('\0').filter(Boolean))
    return
  }
  if (command === 'provenance') {
    const [path, repository, pr, run, baseSha, headSha] = args
    if (!path || !repository || !pr || !run || !baseSha || !headSha) {
      throw new Error('provenance requires artifact path and workflow identity')
    }
    validateProvenance(JSON.parse(await readFile(path, 'utf8')), {
      baseSha,
      headSha,
      pr,
      repository,
      run,
    })
    return
  }
  if (command === 'materialize-nuget') {
    const [trustedPath, candidatePath, metadataPath] = args
    if (!trustedPath || !candidatePath || !metadataPath) throw new Error('materialize-nuget requires trusted, candidate, and metadata paths')
    process.stdout.write(materializeNugetUpdate(await readFile(trustedPath, 'utf8'), await readFile(candidatePath, 'utf8'), await readFile(metadataPath, 'utf8')))
    return
  }
  if (command === 'hash') {
    const [kind, root] = args
    const paths = kind === 'locks' ? dotnetLockPaths : kind === 'android' ? androidRepairPaths : kind === 'candidate' ? candidateHashPaths : undefined
    if (!root || !paths) throw new Error('hash requires locks, android, or candidate and a root')
    process.stdout.write(`${await canonicalHash(root, paths)}\n`)
    return
  }
  throw new Error('Usage: dependabot-repair.mjs <candidate|artifact-paths> ...')
}

if (import.meta.url === `file://${process.argv[1]}`) await main()
