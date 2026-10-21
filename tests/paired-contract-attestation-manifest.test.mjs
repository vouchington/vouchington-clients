import assert from 'node:assert/strict'
import { afterEach, describe, it } from 'node:test'
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { declaredFilamentsContractPaths } from '../scripts/contracts.mjs'

import {
  capturePairSnapshot,
  createPairManifest,
  pairedRepositories,
  validatePairManifest,
} from '../scripts/paired-contract-attestation.mjs'
import {
  clientsBase,
  clientsHead,
  clientsMerge,
  dispatch,
  producerBase,
  producerHead,
  producerMerge,
  snapshotReaders,
} from './paired-contract-attestation-fixtures.mjs'

const temporaryRoots = new Set()

afterEach(async () => {
  await Promise.all([...temporaryRoots].map(path => rm(path, { recursive: true, force: true })))
  temporaryRoots.clear()
})

describe('paired contract exact file evidence', () => {
  it('builds and validates a sorted hash manifest for only the declared contract paths', async () => {
    const stageRoot = await mkdtemp(join(tmpdir(), 'paired-contract-stage-'))
    temporaryRoots.add(stageRoot)
    for (const [index, path] of declaredFilamentsContractPaths.entries()) {
      const directory = join(stageRoot, ...path.split('/'))
      await mkdir(directory, { recursive: true })
      await writeFile(join(directory, `fixture-${index}.txt`), `fixture-${index}\n`)
    }
    const pair = await capturePairSnapshot({ dispatch: dispatch(), ...snapshotReaders() })
    const manifest = await createPairManifest({
      dispatch: dispatch(),
      pair,
      checkoutRevisions: {
        producer: { revision: producerMerge, parents: [producerBase, producerHead] },
        clients: { revision: clientsMerge, parents: [clientsBase, clientsHead] },
      },
      contractConfig: {
        schemaVersion: 1,
        repository: pairedRepositories.producer,
        ref: 'main',
        paths: declaredFilamentsContractPaths,
      },
      stageRoot,
      workflow: {
        repository: pairedRepositories.clients,
        ref: 'refs/heads/main',
        workflowRef: `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main`,
        sha: 'd'.repeat(40),
        runId: '123456',
        runAttempt: 1,
        name: 'Paired native contract review evidence',
      },
    })
    assert.equal(validatePairManifest(manifest), manifest)
    assert.deepEqual(
      manifest.contract.files.map(file => file.path),
      declaredFilamentsContractPaths.map((path, index) => `${path}/fixture-${index}.txt`).sort(),
    )
    assert.equal(manifest.attestation.reviewedBothSnapshots, true)
    const changedDigest = structuredClone(manifest)
    changedDigest.contract.files[0].sha256 = 'z'.repeat(64)
    assert.throws(() => validatePairManifest(changedDigest), /file record is invalid/u)
    const changedRun = structuredClone(manifest)
    changedRun.attestation.workflow.runAttempt = 2
    assert.throws(() => validatePairManifest(changedRun), /workflow identity is invalid/u)
    const changedParent = structuredClone(manifest)
    changedParent.clients.merge.parents[1] = 'e'.repeat(40)
    assert.throws(() => validatePairManifest(changedParent), /snapshot revisions are invalid/u)
    for (const path of [
      'api-fixtures/v1/../secrets.json',
      'api-fixtures/v1/./fixture.json',
      'api-fixtures/v1//fixture.json',
      'api-fixtures/v1\\fixture.json',
    ]) {
      const escapedPath = structuredClone(manifest)
      escapedPath.contract.files[0].path = path
      assert.throws(() => validatePairManifest(escapedPath), /file record is invalid/u)
    }
    const changedOrigin = structuredClone(manifest)
    changedOrigin.contract.outputOrigin = 'trusted'
    assert.throws(() => validatePairManifest(changedOrigin), /contract source/u)
  })

  it('rejects a manifest path outside the canonical allowlist', async () => {
    const stageRoot = await mkdtemp(join(tmpdir(), 'paired-contract-stage-'))
    temporaryRoots.add(stageRoot)
    for (const [index, path] of declaredFilamentsContractPaths.entries()) {
      const directory = join(stageRoot, ...path.split('/'))
      await mkdir(directory, { recursive: true })
      await writeFile(join(directory, `fixture-${index}.txt`), `fixture-${index}\n`)
    }
    await mkdir(join(stageRoot, 'unexpected'), { recursive: true })
    await writeFile(join(stageRoot, 'unexpected', 'payload.txt'), 'not allowed')
    const pair = await capturePairSnapshot({ dispatch: dispatch(), ...snapshotReaders() })
    await assert.rejects(
      createPairManifest({
        dispatch: dispatch(),
        pair,
        checkoutRevisions: {
          producer: { revision: producerMerge, parents: [producerBase, producerHead] },
          clients: { revision: clientsMerge, parents: [clientsBase, clientsHead] },
        },
        contractConfig: {
          schemaVersion: 1,
          repository: pairedRepositories.producer,
          ref: 'main',
          paths: declaredFilamentsContractPaths,
        },
        stageRoot,
        workflow: {
          repository: pairedRepositories.clients,
          ref: 'refs/heads/main',
          sha: 'd'.repeat(40),
          runId: '123456',
          runAttempt: 1,
          name: 'Paired native contract review evidence',
        },
      }),
      /unexpected staged contract path unexpected/u,
    )
  })

  it('rejects extra manifest keys instead of accepting metadata-only rebinding', () => {
    assert.throws(
      () => validatePairManifest({ schemaVersion: 1, ignored: true }),
      /invalid key set/u,
    )
  })
})
