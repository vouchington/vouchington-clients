import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import {
  assertCheckoutMatchesSnapshot,
  assertPairSnapshotUnchanged,
  capturePairSnapshot,
  pairedRepositories,
  validateDispatch,
} from '../scripts/paired-contract-attestation.mjs'

const producerHead = '1'.repeat(40)
const clientsHead = '2'.repeat(40)
const producerBase = '3'.repeat(40)
const clientsBase = '4'.repeat(40)
const producerMerge = '5'.repeat(40)
const clientsMerge = '6'.repeat(40)

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

function snapshotReads(overrides = {}) {
  const readPullRequest = readPair(overrides)
  return {
    readPullRequest,
    readMergeCommit: async (repository, sha) => {
      const pr = await readPullRequest(
        repository,
        repository === pairedRepositories.producer ? '3292' : '376',
      )
      return {
        sha,
        parents: [{ sha: pr.base.sha }, { sha: pr.head.sha }],
      }
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

describe('paired contract immutable snapshots', () => {
  it('captures only open main-target PRs in the two fixed repositories', async () => {
    const pair = await capturePairSnapshot({ dispatch: dispatch(), ...snapshotReads() })
    assert.equal(pair.producer.merge.sha, producerMerge)
    assert.equal(pair.clients.merge.sha, clientsMerge)
    assert.deepEqual(pair.producer.merge.parents, [producerBase, producerHead])
  })

  it('rejects a push that lands before the snapshot is resolved', async () => {
    const changedProducer = pullRequest(pairedRepositories.producer, {
      base: producerBase,
      head: '7'.repeat(40),
      merge: '8'.repeat(40),
    })
    await assert.rejects(
      capturePairSnapshot({
        dispatch: dispatch(),
        ...snapshotReads({ producer: changedProducer }),
      }),
      /live producer head does not match/u,
    )
  })

  it('rejects closed, wrong-repository, and non-main-target pull requests', async () => {
    const invalid = [
      pullRequest(pairedRepositories.clients, {
        base: clientsBase,
        head: clientsHead,
        merge: clientsMerge,
        state: 'closed',
      }),
      {
        ...pullRequest(pairedRepositories.clients, {
          base: clientsBase,
          head: clientsHead,
          merge: clientsMerge,
        }),
        base: { repo: { full_name: 'someone/else' }, ref: 'main', sha: clientsBase },
      },
      pullRequest(pairedRepositories.clients, {
        base: clientsBase,
        head: clientsHead,
        merge: clientsMerge,
        baseRef: 'release',
      }),
    ]
    for (const clients of invalid)
      await assert.rejects(
        capturePairSnapshot({ dispatch: dispatch(), ...snapshotReads({ clients }) }),
        /Paired contract attestation failed/u,
      )
  })

  it('rejects a PR update after resolution or while staging instead of rebinding by metadata', async () => {
    const before = await capturePairSnapshot({ dispatch: dispatch(), ...snapshotReads() })
    const changedHead = await capturePairSnapshot({
      dispatch: dispatch(),
      ...snapshotReads({
        clients: pullRequest(pairedRepositories.clients, {
          base: clientsBase,
          head: '9'.repeat(40),
          merge: 'a'.repeat(40),
        }),
      }),
    }).catch(error => error)
    assert.match(changedHead.message, /live clients head does not match/u)

    const changedBase = await capturePairSnapshot({
      dispatch: dispatch(),
      ...snapshotReads({
        producer: pullRequest(pairedRepositories.producer, {
          base: 'b'.repeat(40),
          head: producerHead,
          merge: 'c'.repeat(40),
        }),
      }),
    })
    assert.throws(() => assertPairSnapshotUnchanged(before, changedBase), /snapshot changed/u)

    // A title/body edit is not a source identity change and cannot rebind the captured hashes.
    const metadataOnly = await capturePairSnapshot({
      dispatch: dispatch(),
      ...snapshotReads(),
    })
    assert.doesNotThrow(() => assertPairSnapshotUnchanged(before, metadataOnly))
  })

  it('requires the checked-out merge commit to have the exact base and head parents', async () => {
    const pair = await capturePairSnapshot({ dispatch: dispatch(), ...snapshotReads() })
    assert.doesNotThrow(() =>
      assertCheckoutMatchesSnapshot(pair.clients, {
        revision: clientsMerge,
        parents: [clientsBase, clientsHead],
      }),
    )
    assert.throws(
      () =>
        assertCheckoutMatchesSnapshot(pair.clients, {
          revision: clientsMerge,
          parents: [clientsHead, clientsBase],
        }),
      /does not have the captured merge parents/u,
    )
  })

  it('uses the merge commit first parent when base metadata moved after merge ref creation', async () => {
    const movedBase = 'a'.repeat(40)
    const mergeParent = 'b'.repeat(40)
    const clients = pullRequest(pairedRepositories.clients, {
      base: movedBase,
      head: clientsHead,
      merge: clientsMerge,
    })
    const readers = snapshotReads({ clients })
    const defaultReadMergeCommit = readers.readMergeCommit
    const defaultReadMain = readers.readMain
    readers.readMergeCommit = async (repository, sha) =>
      repository === pairedRepositories.clients
        ? { sha, parents: [{ sha: mergeParent }, { sha: clientsHead }] }
        : defaultReadMergeCommit(repository, sha)
    readers.readMain = async repository =>
      repository === pairedRepositories.clients ? 'c'.repeat(40) : defaultReadMain(repository)
    readers.isAncestor = async (repository, ancestor, main) =>
      repository === pairedRepositories.clients
        ? ancestor === mergeParent && main === 'c'.repeat(40)
        : true
    const pair = await capturePairSnapshot({ dispatch: dispatch(), ...readers })
    assert.deepEqual(pair.clients.merge.parents, [mergeParent, clientsHead])
    assert.equal(pair.clients.base.sha, movedBase)
    assert.equal(pair.clients.main.sha, 'c'.repeat(40))
  })

  it('rejects merge candidates with a substituted PR parent or a base outside live main', async () => {
    const readers = snapshotReads()
    readers.readMergeCommit = async (repository, sha) => ({
      sha,
      parents: [
        { sha: producerBase },
        { sha: repository === pairedRepositories.clients ? 'd'.repeat(40) : producerHead },
      ],
    })
    await assert.rejects(
      capturePairSnapshot({ dispatch: dispatch(), ...readers }),
      /second parent does not match/u,
    )

    const nonAncestor = snapshotReads()
    nonAncestor.isAncestor = async () => false
    await assert.rejects(
      capturePairSnapshot({ dispatch: dispatch(), ...nonAncestor }),
      /not an ancestor of live main/u,
    )
  })
})
