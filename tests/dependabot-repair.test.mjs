import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import {
  androidRepairPaths,
  dotnetLockPaths,
  materializeNugetUpdate,
  validateCandidate,
  validateCandidatePaths,
  validateProvenance,
  validatePublishedArtifact,
  validatePublishedPaths,
} from '../scripts/dependabot-repair.mjs'

const sha = character => character.repeat(40)

describe('trusted Dependabot repair boundary', () => {
  it('accepts only an open same-repository Dependabot branch at the exact event identity', () => {
    const candidate = {
      base: { ref: 'main', sha: sha('a') },
      head: { ref: 'dependabot/nuget/xunit-3.0.0', repo: { full_name: 'vouchington/vouchington-clients' }, sha: sha('b') },
      state: 'open',
      user: { login: 'dependabot[bot]' },
    }
    assert.doesNotThrow(() => validateCandidate(candidate, {
      baseSha: sha('a'), defaultBranch: 'main', headRef: 'dependabot/nuget/xunit-3.0.0', headSha: sha('b'), repository: 'vouchington/vouchington-clients',
    }))
  })

  for (const [name, mutate] of [
    ['spoofed author', candidate => { candidate.user.login = 'attacker' }],
    ['changed base', candidate => { candidate.base.sha = sha('c') }],
    ['fork head', candidate => { candidate.head.repo.full_name = 'attacker/repo' }],
    ['non-Dependabot head', candidate => { candidate.head.ref = 'feature/repair' }],
  ]) {
    it(`rejects ${name} before repair`, () => {
      const candidate = {
        base: { ref: 'main', sha: sha('a') },
        head: { ref: 'dependabot/nuget/xunit-3.0.0', repo: { full_name: 'vouchington/vouchington-clients' }, sha: sha('b') },
        state: 'open', user: { login: 'dependabot[bot]' },
      }
      mutate(candidate)
      assert.throws(() => validateCandidate(candidate, {
        baseSha: sha('a'), defaultBranch: 'main', headRef: 'dependabot/nuget/xunit-3.0.0', headSha: sha('b'), repository: 'vouchington/vouchington-clients',
      }))
    })
  }

  it('allows only the seven NuGet locks and Android materializer in a verified artifact', () => {
    assert.deepEqual(dotnetLockPaths, [
      'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
      'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
      'dotnet-clients/src/Voucha.Client.Core/packages.lock.json',
      'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
      'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
      'dotnet-clients/tests/Voucha.Client.App.Tests/packages.lock.json',
      'dotnet-clients/tests/Voucha.Client.Core.Tests/packages.lock.json',
    ])
    assert.deepEqual(androidRepairPaths, ['swift-clients/apps/android/tooling/materialize-skip-sdk.sh'])
    assert.doesNotThrow(() => validatePublishedArtifact([...dotnetLockPaths, ...androidRepairPaths]))
    assert.throws(() => validatePublishedArtifact([...dotnetLockPaths, 'package.json']))
    assert.throws(() => validatePublishedArtifact([...dotnetLockPaths]))
  })

  it('admits only declared native dependency inputs from the untrusted head', () => {
    assert.doesNotThrow(() => validateCandidatePaths(['dotnet-clients/Directory.Packages.props']))
    assert.doesNotThrow(() => validateCandidatePaths([
      'swift-clients/apps/android/Package.resolved',
      'swift-clients/apps/android/Package.swift',
      'swift-clients/apps/android/tooling/materialize-skip-sdk.sh',
    ]))
    assert.throws(() => validateCandidatePaths(['package.json']))
    assert.throws(() => validateCandidatePaths([dotnetLockPaths[0]]))
    assert.throws(() => validateCandidatePaths([
      'dotnet-clients/Directory.Packages.props',
      'swift-clients/apps/android/Package.swift',
    ]))
    assert.throws(() => validateCandidatePaths([]))
  })

  it('rejects artifact provenance from a different run or malformed hashes', () => {
    const expected = { baseSha: sha('a'), headSha: sha('b'), pr: '42', repository: 'vouchington/vouchington-clients', run: '99' }
    const provenance = { ...expected, candidateHash: 'b'.repeat(64), locksHash: 'c'.repeat(64), androidHash: 'd'.repeat(64) }
    assert.doesNotThrow(() => validateProvenance(provenance, expected))
    assert.throws(() => validateProvenance({ ...provenance, run: '100' }, expected))
    assert.throws(() => validateProvenance({ ...provenance, locksHash: 'not-a-hash' }, expected))
  })

  it('allows the publisher to commit a nonempty subset of generated outputs', () => {
    assert.doesNotThrow(() => validatePublishedPaths([dotnetLockPaths[0]]))
    assert.doesNotThrow(() => validatePublishedPaths(androidRepairPaths))
    assert.throws(() => validatePublishedPaths([]))
    assert.throws(() => validatePublishedPaths(['README.md']))
  })

  it('reconstructs NuGet updates from trusted literals after rejecting executable candidate XML', () => {
    const trusted = '<Project>\n<PackageVersion Include="Example.One" Version="1.2.3" />\n</Project>\n'
    const candidate = trusted.replace('1.2.3', '1.2.4')
    const metadata = JSON.stringify([{ dependencyName: 'Example.One', prevVersion: '1.2.3', newVersion: '1.2.4' }])
    assert.equal(materializeNugetUpdate(trusted, candidate, metadata), candidate)
    assert.throws(() => materializeNugetUpdate(trusted, candidate.replace('</Project>', '<Target Name="Injected" /></Project>'), metadata))
  })
})
