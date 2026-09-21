import { lstat, mkdir, mkdtemp, readFile, rm, symlink, writeFile } from 'node:fs/promises'
import assert from 'node:assert/strict'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import test from 'node:test'

import { createContractArtifact, verifyContractArtifact } from '../scripts/contract-artifact.mjs'

const paths = [
  'api-fixtures/v1',
  'swift-clients/ui/Sources/VouchaLocalization/Generated',
  'dotnet-clients/src/Voucha.Client.Core/Localization/Generated',
]

async function writeTree(root, files) {
  for (const [path, contents] of Object.entries(files)) {
    const file = join(root, path)
    await mkdir(dirname(file), { recursive: true })
    await writeFile(file, contents)
  }
}

async function fixture(t) {
  const filamentsRoot = await mkdtemp(join(tmpdir(), 'voucha-artifact-filaments-'))
  const artifactRoot = join(await mkdtemp(join(tmpdir(), 'voucha-artifact-')), 'artifact')
  t.after(() => rm(filamentsRoot, { recursive: true, force: true }))
  t.after(() => rm(dirname(artifactRoot), { recursive: true, force: true }))
  await writeTree(filamentsRoot, {
    'api-fixtures/v1/manifest.json': '{"fixtures":[]}\n',
    'swift-clients/ui/Sources/VouchaLocalization/Generated/UiMessageKey.swift':
      'enum UiMessageKey {}\n',
    'dotnet-clients/src/Voucha.Client.Core/Localization/Generated/UiMessageKey.g.cs':
      'class UiMessageKey {}\n',
  })
  const identity = {
    filamentsRoot,
    outputRoot: artifactRoot,
    filamentsSha: 'a'.repeat(40),
    clientsRepository: 'vouchington/vouchington-clients',
    candidateEvent: 'pull_request',
    candidateNumber: 17,
    baseSha: 'b'.repeat(40),
    headSha: 'c'.repeat(40),
    revisionSha: 'd'.repeat(40),
    producerRunId: '12345',
    producerRunAttempt: '2',
  }
  return identity
}

function expected(identity) {
  return {
    artifactRoot: identity.outputRoot,
    expectedClientsRepository: identity.clientsRepository,
    expectedFilamentsSha: identity.filamentsSha,
    expectedCandidateEvent: identity.candidateEvent,
    expectedCandidateNumber: identity.candidateNumber,
    expectedBaseSha: identity.baseSha,
    expectedHeadSha: identity.headSha,
    expectedRevisionSha: identity.revisionSha,
    expectedProducerRunId: identity.producerRunId,
    expectedProducerRunAttempt: identity.producerRunAttempt,
  }
}

test('creates a narrow, deterministic manifest and verifies it', async t => {
  const identity = await fixture(t)
  const manifest = await createContractArtifact(identity)
  assert.deepEqual(manifest.allowlistedPaths, [...paths].sort())
  assert.deepEqual(
    manifest.files.map(file => file.path),
    manifest.files.map(file => file.path).toSorted(),
  )
  assert.equal(manifest.files.length, 3)
  assert.equal((await verifyContractArtifact(expected(identity))).files.length, 3)
  assert.deepEqual(
    JSON.parse(await readFile(join(identity.outputRoot, 'manifest.json'), 'utf8')),
    manifest,
  )
})

test('records an explicitly trusted producer without changing the trusted path allowlist', async t => {
  const identity = {
    ...(await fixture(t)),
    contractRepository: 'example/contracts',
  }
  const manifest = await createContractArtifact(identity)

  assert.equal(manifest.filaments.repository, identity.contractRepository)
  assert.deepEqual(manifest.allowlistedPaths, [...paths].sort())
  await verifyContractArtifact({
    ...expected(identity),
    expectedContractRepository: identity.contractRepository,
  })
  await assert.rejects(
    verifyContractArtifact({
      ...expected(identity),
      expectedContractRepository: 'vouchington/vouchington',
    }),
    /invalid manifest Filaments repository/,
  )
})

test('uses the verifier ordinal order for punctuation-bearing paths', async t => {
  const identity = await fixture(t)
  await writeTree(identity.filamentsRoot, {
    'api-fixtures/v1/a-.json': '{}\n',
    'api-fixtures/v1/a_.json': '{}\n',
  })

  const manifest = await createContractArtifact(identity)
  assert.deepEqual(
    manifest.files.map(file => file.path).filter(path => /a[-_]\.json$/u.test(path)),
    ['api-fixtures/v1/a-.json', 'api-fixtures/v1/a_.json'],
  )
  await verifyContractArtifact(expected(identity))
})

test('creates and verifies a main-branch push identity without a pull request', async t => {
  const identity = {
    ...(await fixture(t)),
    candidateEvent: 'push',
    candidateNumber: undefined,
    headSha: 'd'.repeat(40),
    revisionSha: 'd'.repeat(40),
  }
  const manifest = await createContractArtifact(identity)
  assert.deepEqual(manifest.candidate, {
    event: 'push',
    number: null,
    base: identity.baseSha,
    head: identity.headSha,
    revision: identity.revisionSha,
  })
  assert.equal((await verifyContractArtifact(expected(identity))).candidate.event, 'push')
})

test('rejects tampered content, size, unexpected paths, and symlinks', async t => {
  const identity = await fixture(t)
  await createContractArtifact(identity)
  const target = join(identity.outputRoot, paths[0], 'manifest.json')
  await writeFile(target, 'tampered\n')
  await assert.rejects(verifyContractArtifact(expected(identity)), /hash mismatch|size mismatch/)
  await createContractArtifact({
    ...identity,
    outputRoot: join(dirname(identity.outputRoot), 'second'),
  })
  const second = { ...identity, outputRoot: join(dirname(identity.outputRoot), 'second') }
  await writeFile(join(second.outputRoot, 'unexpected.txt'), 'nope\n')
  await assert.rejects(verifyContractArtifact(expected(second)), /unexpected artifact path/)
  await rm(join(second.outputRoot, 'unexpected.txt'))
  await symlink('manifest.json', join(second.outputRoot, 'link.json'))
  await assert.rejects(verifyContractArtifact(expected(second)), /symbolic link/)
})

test('rejects metadata identity changes and malformed manifests', async t => {
  const identity = await fixture(t)
  await createContractArtifact(identity)
  await assert.rejects(
    verifyContractArtifact({ ...expected(identity), expectedHeadSha: 'e'.repeat(40) }),
    /head SHA mismatch/,
  )
  await assert.rejects(
    verifyContractArtifact({ ...expected(identity), expectedCandidateEvent: 'push' }),
    /candidate event mismatch/,
  )
  const manifestPath = join(identity.outputRoot, 'manifest.json')
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'))
  manifest.files[0].path = '../outside'
  await writeFile(manifestPath, JSON.stringify(manifest))
  await assert.rejects(verifyContractArtifact(expected(identity)), /invalid artifact path/)
})

test('rejects symlinked Filaments sources before copying', async t => {
  const identity = await fixture(t)
  await symlink(
    'UiMessageKey.swift',
    join(
      identity.filamentsRoot,
      'swift-clients/ui/Sources/VouchaLocalization/Generated/alias.swift',
    ),
  )
  await assert.rejects(createContractArtifact(identity), /symbolic link/)
  await assert.rejects(lstat(identity.outputRoot), { code: 'ENOENT' })
})
