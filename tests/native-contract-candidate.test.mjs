import assert from 'node:assert/strict'
import { describe, it } from 'node:test'

import { validatePullRequestCandidate } from '../scripts/validate-native-contract-candidate.mjs'

const oldBase = '1de5b206ab373f8a5f29bd46bcc43c199dd59901'
const currentBase = '574096a3d51a21f2e3a5f48601ae4428673955ac'
const currentHead = '0dd279ecee602e19a66f97c1b9f707cc372ed275'
const currentRevision = 'a'.repeat(40)
const staleRevision = 'f1878c624a5d1da4cd6dfd90f18676dea24f0160'

function makeCandidate({
  event = 'pull_request',
  base = currentBase,
  head = currentHead,
  revision = currentRevision,
} = {}) {
  return { event, number: 338, base, head, revision }
}

function validate(candidate) {
  return validatePullRequestCandidate({
    candidate,
    event: { number: '338', baseSha: currentBase, headSha: currentHead },
    readPullRequest: () => ({
      state: 'open',
      base: { sha: currentBase },
      head: { sha: currentHead },
    }),
    readCurrentMergeRevision: () => currentRevision,
  })
}

function validateWith({
  candidate = makeCandidate(),
  event = { number: '338', baseSha: currentBase, headSha: currentHead },
  pullRequest = {
    state: 'open',
    base: { sha: currentBase },
    head: { sha: currentHead },
  },
  revision = currentRevision,
} = {}) {
  return validatePullRequestCandidate({
    candidate,
    event,
    readPullRequest: () => pullRequest,
    readCurrentMergeRevision: () => revision,
  })
}

describe('native contract pull-request candidate validation', () => {
  it('rejects an artifact created for a different event kind', () => {
    assert.throws(
      () => validateWith({ candidate: makeCandidate({ event: 'push' }) }),
      /artifact is not for a pull request/u,
    )
  })

  it('rejects an artifact for a different pull request number', () => {
    assert.throws(
      () => validateWith({ candidate: { ...makeCandidate(), number: 337 } }),
      /pull request number changed/u,
    )
  })

  it('rejects an old base when event and live PR metadata have advanced', () => {
    let readPullRequest = false

    assert.throws(
      () =>
        validatePullRequestCandidate({
          candidate: makeCandidate({ base: oldBase }),
          event: { number: '338', baseSha: currentBase, headSha: currentHead },
          readPullRequest: () => {
            readPullRequest = true
            return {
              state: 'open',
              base: { sha: currentBase },
              head: { sha: currentHead },
            }
          },
          readCurrentMergeRevision: () => currentRevision,
        }),
      /event base SHA changed/u,
    )
    assert.equal(readPullRequest, false)
  })

  it('accepts a fresh candidate only when live PR and merge revision match', () => {
    assert.deepEqual(validate(makeCandidate()), {
      baseSha: currentBase,
      revisionSha: currentRevision,
    })
  })

  it('rejects a stale head even when base and revision are current', () => {
    assert.throws(
      () => validate(makeCandidate({ head: '1'.repeat(40) })),
      /event head SHA changed/u,
    )
  })

  it('rejects a live PR whose head advanced after the event', () => {
    assert.throws(
      () =>
        validateWith({
          pullRequest: {
            state: 'open',
            base: { sha: currentBase },
            head: { sha: 'b'.repeat(40) },
          },
        }),
      /pull request head SHA changed/u,
    )
  })

  it('rejects a closed live PR', () => {
    assert.throws(
      () => validateWith({ pullRequest: { state: 'closed' } }),
      /pull request is closed/u,
    )
  })

  it('does not let updated live metadata rescue an old-base event artifact', () => {
    assert.throws(
      () =>
        validatePullRequestCandidate({
          candidate: makeCandidate({ base: oldBase }),
          event: { number: '338', baseSha: oldBase, headSha: currentHead },
          readPullRequest: () => ({
            state: 'open',
            base: { sha: currentBase },
            head: { sha: currentHead },
          }),
          readCurrentMergeRevision: () => currentRevision,
        }),
      /pull request base SHA changed/u,
    )
  })

  it('rejects a stale merge revision after the event and live PR match', () => {
    assert.throws(
      () =>
        validatePullRequestCandidate({
          candidate: makeCandidate({ revision: staleRevision }),
          event: { number: '338', baseSha: currentBase, headSha: currentHead },
          readPullRequest: () => ({
            state: 'open',
            base: { sha: currentBase },
            head: { sha: currentHead },
          }),
          readCurrentMergeRevision: () => currentRevision,
        }),
      /pull request merge SHA changed/u,
    )
  })
})
