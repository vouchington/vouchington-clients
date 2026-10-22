import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { pairedRepositories } from '../scripts/paired-contract-attestation.mjs'
import {
  clientsBase,
  clientsHead,
  clientsMerge,
  dispatch,
  producerBase,
  producerHead,
  producerMerge,
  pullRequest,
  snapshotReaders,
} from './paired-contract-attestation-fixtures.mjs'

describe('paired contract attestation fixtures', () => {
  it('builds a main dispatch and keeps unspecified inputs', () => {
    const value = dispatch()
    assert.equal(value.actor, 'maintainer')
    assert.equal(value.clientsPermission, 'maintain')
    assert.equal(value.producerNumber, '3292')
    assert.equal(value.clientsNumber, '376')
    assert.equal(value.expectedProducerHeadSha, producerHead)
    assert.equal(value.expectedClientsHeadSha, clientsHead)
    assert.equal(value.nativeTestProfile, 'cookie-snapshot')

    const overridden = dispatch({ inputs: { nativeTestProfile: 'pending-turn-recovery' } })
    assert.equal(overridden.nativeTestProfile, 'pending-turn-recovery')
    assert.equal(overridden.expectedProducerHeadSha, producerHead)
    assert.equal(overridden.expectedClientsHeadSha, clientsHead)
  })

  it('builds open main-target pull requests on a fork head', () => {
    const producer = pullRequest(pairedRepositories.producer, {
      base: producerBase,
      head: producerHead,
      merge: producerMerge,
    })
    assert.equal(producer.number, 3292)
    assert.equal(producer.state, 'open')
    assert.equal(producer.base.ref, 'main')
    assert.equal(producer.base.repo.full_name, pairedRepositories.producer)
    assert.equal(producer.head.repo.full_name, `${pairedRepositories.producer}-fork`)
    assert.equal(producer.merge_commit_sha, producerMerge)

    const clients = pullRequest(pairedRepositories.clients, {
      base: clientsBase,
      head: clientsHead,
      merge: clientsMerge,
      state: 'closed',
      baseRef: 'release',
    })
    assert.equal(clients.number, 376)
    assert.equal(clients.state, 'closed')
    assert.equal(clients.base.ref, 'release')
  })

  it('reads the fixed pair and rejects an unexpected pull request', async () => {
    const readers = snapshotReaders()
    const clients = await readers.readPullRequest(pairedRepositories.clients, '376')
    assert.equal(clients.base.sha, clientsBase)
    assert.equal(clients.head.sha, clientsHead)
    const merge = await readers.readMergeCommit(pairedRepositories.clients, clientsMerge)
    assert.deepEqual(merge, {
      sha: clientsMerge,
      parents: [{ sha: clientsBase }, { sha: clientsHead }],
    })
    assert.equal(await readers.readMain(pairedRepositories.producer), producerBase)
    assert.equal(await readers.isAncestor(), true)

    const custom = pullRequest(pairedRepositories.producer, {
      base: 'a'.repeat(40),
      head: 'b'.repeat(40),
      merge: 'c'.repeat(40),
    })
    const overridden = snapshotReaders({ producer: custom })
    assert.equal(await overridden.readPullRequest(pairedRepositories.producer, '3292'), custom)

    await assert.rejects(
      readers.readPullRequest('someone/else', '1'),
      /unexpected pull request someone\/else#1/u,
    )
  })
})
