import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { describe, it } from 'node:test'
import { promisify } from 'node:util'
import {
  NUGET_LOCK_PATHS,
  validateDotnetRepairProvenance,
  validateDotnetRepairPublishedPaths,
  validateDotnetRepairPullRequest,
  validateDotnetRepairRawDiff,
} from '../scripts/validate-dependabot-dotnet-repair.mjs'
import {
  runDependabotNugetUpdateCli,
  validateDependabotNugetUpdate,
} from '../scripts/validate-dependabot-nuget-update.mjs'

const execFileAsync = promisify(execFile)
const repositoryRoot = resolve(import.meta.dirname, '..')
const repairScript = join(repositoryRoot, 'scripts/validate-dependabot-dotnet-repair.mjs')
const repository = 'jonathanong/vouchington-clients'
const defaultBranch = 'main'
const headRef = 'dependabot/nuget/dotnet-clients/example-2.0.0'
const baseSha = 'a'.repeat(40)
const headSha = 'b'.repeat(40)

function livePullRequest(overrides = {}) {
  return {
    number: 52,
    changed_files: 1,
    state: 'open',
    draft: false,
    user: { login: 'dependabot[bot]' },
    base: { ref: defaultBranch, sha: baseSha, repo: { full_name: repository } },
    head: { ref: headRef, sha: headSha, repo: { full_name: repository } },
    ...overrides,
  }
}

function provenance(overrides = {}) {
  return {
    version: 1,
    repository,
    pullRequest: 52,
    workflowRun: 819,
    baseSha,
    headSha,
    manifestSha256: 'f'.repeat(64),
    locks: NUGET_LOCK_PATHS.map((path, index) => ({
      path,
      sha256: String(index).padStart(64, '0'),
    })),
    ...overrides,
  }
}

function centralVersions(versions) {
  return `<Project>\n  <ItemGroup>\n${Object.entries(versions)
    .map(([name, version]) => `    <PackageVersion Include="${name}" Version="${version}" />`)
    .join('\n')}\n  </ItemGroup>\n</Project>\n`
}

describe('trusted Dependabot .NET repair validation', () => {
  it('accepts only a same-repository live Dependabot PR and its one allowed raw-diff entry', () => {
    assert.deepEqual(
      validateDotnetRepairPullRequest(
        livePullRequest(),
        ':100644 100644 aaaaaaa bbbbbbb M\0dotnet-clients/Directory.Packages.props\0',
        defaultBranch,
        repository,
        headRef,
        baseSha,
        headSha,
      ),
      { number: 52, baseSha, headSha, changedPaths: ['dotnet-clients/Directory.Packages.props'] },
    )
  })

  it('permits the manifest with any existing lock subset and checks GitHub changed_files', () => {
    const changedPaths = [
      NUGET_LOCK_PATHS[0],
      'dotnet-clients/Directory.Packages.props',
      NUGET_LOCK_PATHS.at(-1),
    ]
    const rawDiff = changedPaths.map(path => `:100644 100644 aaaaaaa bbbbbbb M\0${path}\0`).join('')
    assert.deepEqual(validateDotnetRepairRawDiff(rawDiff), changedPaths)
    assert.throws(
      () =>
        validateDotnetRepairPullRequest(
          livePullRequest({ changed_files: 2 }),
          rawDiff,
          defaultBranch,
          repository,
          headRef,
          baseSha,
          headSha,
        ),
      /changed_files/u,
    )
  })

  it('rejects spoofed PR identities, stale SHAs, and non-open PRs', () => {
    const cases = [
      [livePullRequest({ user: { login: 'dependabot' } }), /author/u],
      [
        livePullRequest({
          head: { ref: headRef, sha: headSha, repo: { full_name: 'fork/example' } },
        }),
        /head repository/u,
      ],
      [
        livePullRequest({
          base: { ref: 'release', sha: baseSha, repo: { full_name: repository } },
        }),
        /base branch/u,
      ],
      [
        livePullRequest({
          head: { ref: headRef, sha: 'c'.repeat(40), repo: { full_name: repository } },
        }),
        /head SHA/u,
      ],
      [livePullRequest({ state: 'closed' }), /state/u],
      [livePullRequest({ draft: true }), /draft/u],
    ]
    for (const [pullRequest, message] of cases) {
      assert.throws(
        () =>
          validateDotnetRepairPullRequest(
            pullRequest,
            ':100644 100644 aaaaaaa bbbbbbb M\0dotnet-clients/Directory.Packages.props\0',
            defaultBranch,
            repository,
            headRef,
            baseSha,
            headSha,
          ),
        message,
      )
    }
  })

  it('rejects malformed, additive, renamed, or expanded raw diffs', () => {
    for (const rawDiff of [
      ':100644 100644 aaaaaaa bbbbbbb M\0dotnet-clients/Directory.Packages.props',
      ':100644 100644 aaaaaaa bbbbbbb A\0dotnet-clients/Directory.Packages.props\0',
      ':100644 100644 aaaaaaa bbbbbbb R100\0dotnet-clients/other.props\0dotnet-clients/Directory.Packages.props\0',
      ':100644 100644 aaaaaaa bbbbbbb M\0dotnet-clients/Directory.Packages.props\0:100644 100644 aaaaaaa bbbbbbb M\0README.md\0',
      ':100644 100644 aaaaaaa bbbbbbb M\0dotnet-clients/Directory.Packages.props\0\0',
    ]) {
      assert.throws(
        () => validateDotnetRepairRawDiff(rawDiff),
        /complete|regular-file|may change|NUL-delimited/u,
      )
    }
  })

  it('validates the exact seven-lock provenance shape and trusted context', () => {
    const locks = validateDotnetRepairProvenance(
      provenance(),
      repository,
      '52',
      '819',
      baseSha,
      headSha,
    )
    assert.deepEqual(
      locks.map(lock => lock.path),
      NUGET_LOCK_PATHS,
    )

    const cases = [
      [provenance({ repository: 'fork/example' }), /repository/u],
      [provenance({ workflowRun: 820 }), /workflow run/u],
      [provenance({ headSha: 'c'.repeat(40) }), /head SHA/u],
      [provenance({ manifestSha256: 'F'.repeat(64) }), /manifest hash/u],
      [provenance({ extra: true }), /unexpected fields/u],
      [provenance({ locks: provenance().locks.slice(0, -1) }), /exactly seven/u],
      [
        provenance({
          locks: [
            { path: NUGET_LOCK_PATHS[0], sha256: 'A'.repeat(64) },
            ...provenance().locks.slice(1),
          ],
        }),
        /SHA-256/u,
      ],
      [
        provenance({
          locks: [{ path: 'README.md', sha256: '0'.repeat(64) }, ...provenance().locks.slice(1)],
        }),
        /path/u,
      ],
    ]
    for (const [candidate, message] of cases) {
      assert.throws(
        () => validateDotnetRepairProvenance(candidate, repository, '52', '819', baseSha, headSha),
        message,
      )
    }
  })

  it('permits publication of any nonempty committed lock subset', () => {
    const paths = `${NUGET_LOCK_PATHS.join('\0')}\0`
    assert.deepEqual(validateDotnetRepairPublishedPaths(paths), NUGET_LOCK_PATHS)
    assert.deepEqual(validateDotnetRepairPublishedPaths(`${NUGET_LOCK_PATHS[0]}\0`), [
      NUGET_LOCK_PATHS[0],
    ])
    const reversed = [...NUGET_LOCK_PATHS].reverse()
    assert.deepEqual(validateDotnetRepairPublishedPaths(`${reversed.join('\0')}\0`), reversed)
    for (const invalidPaths of [
      `${NUGET_LOCK_PATHS.join('\0')}\0README.md\0`,
      `${[NUGET_LOCK_PATHS[0], NUGET_LOCK_PATHS[0], ...NUGET_LOCK_PATHS.slice(2)].join('\0')}\0`,
      NUGET_LOCK_PATHS.join('\0'),
    ]) {
      assert.throws(
        () => validateDotnetRepairPublishedPaths(invalidPaths),
        /only the seven|unique|NUL-delimited/u,
      )
    }
  })
})

describe('Dependabot NuGet candidate manifest', () => {
  const trustedSource = centralVersions({ Alpha: '1.0.0', Beta: '2.0.0' })
  const candidateSource = centralVersions({ Alpha: '1.1.0', Beta: '2.0.0' })
  const metadata = JSON.stringify([
    { dependencyName: 'alpha', prevVersion: '1.0.0', newVersion: '1.1.0' },
  ])

  it('delegates literal central-version validation to vouchington-tooling', () => {
    assert.deepEqual(validateDependabotNugetUpdate(trustedSource, candidateSource, metadata), [
      'Alpha',
    ])
    assert.throws(
      () =>
        validateDependabotNugetUpdate(
          trustedSource,
          candidateSource.replace('  </ItemGroup>', '    <PropertyGroup />\n  </ItemGroup>'),
          metadata,
        ),
      /only literal central PackageVersion values/u,
    )
  })

  it('writes a validated candidate exactly once', async t => {
    const directory = await mkdtemp(join(tmpdir(), 'voucha-nuget-candidate-'))
    const trustedPath = join(directory, 'trusted.props')
    const candidatePath = join(directory, 'candidate.props')
    const metadataPath = join(directory, 'metadata.json')
    const outputPath = join(directory, 'output.props')
    t.after(() => rm(directory, { recursive: true, force: true }))
    await Promise.all([
      writeFile(trustedPath, trustedSource),
      writeFile(candidatePath, candidateSource),
      writeFile(metadataPath, metadata),
    ])

    assert.deepEqual(
      await runDependabotNugetUpdateCli([trustedPath, candidatePath, metadataPath, outputPath]),
      ['Alpha'],
    )
    assert.equal(await readFile(outputPath, 'utf8'), candidateSource)
    await assert.rejects(
      runDependabotNugetUpdateCli([trustedPath, candidatePath, metadataPath, outputPath]),
      /EEXIST/u,
    )
  })

  it('reads raw NUL-delimited inputs through the command-line repair modes', async t => {
    const directory = await mkdtemp(join(tmpdir(), 'voucha-dotnet-repair-cli-'))
    const pullRequestPath = join(directory, 'pull-request.json')
    const rawDiffPath = join(directory, 'raw-diff')
    const provenancePath = join(directory, 'provenance.json')
    const pathsPath = join(directory, 'paths')
    t.after(() => rm(directory, { recursive: true, force: true }))
    await Promise.all([
      writeFile(pullRequestPath, JSON.stringify(livePullRequest())),
      writeFile(
        rawDiffPath,
        ':100644 100644 aaaaaaa bbbbbbb M\0dotnet-clients/Directory.Packages.props\0',
      ),
      writeFile(provenancePath, JSON.stringify(provenance())),
      writeFile(pathsPath, `${NUGET_LOCK_PATHS.join('\0')}\0`),
    ])

    await execFileAsync('node', [
      repairScript,
      'pull-request',
      pullRequestPath,
      rawDiffPath,
      defaultBranch,
      repository,
      headRef,
      baseSha,
      headSha,
    ])
    await execFileAsync('node', [
      repairScript,
      'provenance',
      provenancePath,
      repository,
      '52',
      '819',
      baseSha,
      headSha,
    ])
    await execFileAsync('node', [repairScript, 'published-paths', pathsPath])
  })
})
