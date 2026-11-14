import assert from 'node:assert/strict'
import { pairedRepositories, validateDispatch } from '../scripts/paired-contract-attestation.mjs'

export const producerHead = '1'.repeat(40)
export const clientsHead = '2'.repeat(40)
export const producerBase = '3'.repeat(40)
export const clientsBase = '4'.repeat(40)
export const producerMerge = '5'.repeat(40)
export const clientsMerge = '6'.repeat(40)

export function dispatch(overrides = {}) {
  const inputs = {
    producerPullRequest: '3292',
    expectedProducerHeadSha: producerHead,
    clientsPullRequest: '376',
    expectedClientsHeadSha: clientsHead,
    nativeTestProfile: 'cookie-snapshot',
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

export function pullRequest(repository, { base, head, merge, state = 'open', baseRef = 'main' }) {
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

export function snapshotReaders(overrides = {}) {
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
