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
  validateDispatch,
  validatePairManifest,
} from '../scripts/paired-contract-attestation.mjs'

const producerHead = '1'.repeat(40)
const clientsHead = '2'.repeat(40)
const producerBase = '3'.repeat(40)
const clientsBase = '4'.repeat(40)
const producerMerge = '5'.repeat(40)
const clientsMerge = '6'.repeat(40)
const temporaryRoots = new Set()

afterEach(async () => {
  await Promise.all([...temporaryRoots].map(path => rm(path, { recursive: true, force: true })))
  temporaryRoots.clear()
})

function dispatch(overrides = {}) {
  const inputs = {
    producerPullRequest: '3292',
    expectedProducerHeadSha: producerHead,
    clientsPullRequest: '376',
    expectedClientsHeadSha: clientsHead,
    reviewedBothSnapshots: true,
    ...overrides.inputs,
  }
  const { inputs: ignoredInputs, ...fields } = overrides
  void ignoredInputs
  return validateDispatch({
    eventName: 'workflow_dispatch',
    repository: pairedRepositories.clients,
    ref: 'refs/heads/main',
    workflowRef: `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main`,
    runAttempt: 1,
    actor: 'maintainer',
    triggeringActor: 'maintainer',
    clientsPermission: 'maintain',
    inputs,
    ...fields,
  })
}

function pullRequest(repository, { base, head, merge, state = 'open', baseRef = 'main' }) {
  return {
    number: repository === pairedRepositories.producer ? 3292 : 376,
    state,
    base: { repo: { full_name: repository }, ref: baseRef, sha: base },
    head: { repo: { full_name: `${repository}-fork` }, sha: head },
    merge_commit_sha: merge,
  }
}

function readPair(overrides = {}) {
  return async (repository, number) => {
    if (repository === pairedRepositories.producer && number === '3292')
      return (
        overrides.producer ??
        pullRequest(repository, { base: producerBase, head: producerHead, merge: producerMerge })
      )
    if (repository === pairedRepositories.clients && number === '376')
      return (
        overrides.clients ??
        pullRequest(repository, { base: clientsBase, head: clientsHead, merge: clientsMerge })
      )
    assert.fail(`unexpected pull request ${repository}#${number}`)
  }
}

function snapshotReaders(overrides = {}) {
  const readPullRequest = readPair(overrides)
  return {
    readPullRequest,
    readMergeCommit: async (repository, sha) => {
      const pr = await readPullRequest(
        repository,
        repository === pairedRepositories.producer ? '3292' : '376',
      )
      return { sha, parents: [{ sha: pr.base.sha }, { sha: pr.head.sha }] }
    },
    readMain: async repository => {
      const pr = await readPullRequest(
        repository,
        repository === pairedRepositories.producer ? '3292' : '376',
      )
      return pr.base.sha
    },
    isAncestor: async () => true,
  }
}

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
