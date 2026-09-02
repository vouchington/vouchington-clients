import assert from 'node:assert/strict'
import test from 'node:test'

import {
  CHECKPOINT_MARKER,
  HARNESS_SESSION_ID,
  isTrustedCheckpointComment,
  parseCheckpoint,
  renderCheckpoint,
  sortedCheckpointCandidates,
  validateCheckpoint,
} from '../scripts/harness-shepherd-checkpoint.mjs'

const checkpoint = {
  marker: CHECKPOINT_MARKER,
  repository: 'vouchington/vouchington-clients',
  pr: 42,
  triggerCommentId: 7,
  headRef: 'refs/heads/fix-issue-42',
  startSha: 'a'.repeat(40),
  sessionStartSha: 'a'.repeat(40),
  runId: '123',
  runUrl: 'https://github.com/vouchington/vouchington-clients/actions/runs/123',
  actor: 'octocat',
  sessionId: 'sess-0123abcd',
  sessionUrl: 'https://harness.example.com/sessions/sess-0123abcd',
  resumeSourceRunId: '123',
  status: 'running',
  createdAt: '2026-09-01T00:00:00.000Z',
  updatedAt: '2026-09-01T00:00:00.000Z',
}

test('CHECKPOINT_MARKER is the vouchington-clients-scoped marker, not the published default', () => {
  assert.equal(CHECKPOINT_MARKER, 'shepherd-checkpoint:v1')
})

test('HARNESS_SESSION_ID matches production session ids and rejects malformed ones', () => {
  assert.equal(HARNESS_SESSION_ID.test('sess-0123abcd'), true)
  assert.equal(HARNESS_SESSION_ID.test('sess-123'), false)
  assert.equal(HARNESS_SESSION_ID.test('SESS-0123ABCD'), false)
})

test('renderCheckpoint and parseCheckpoint round-trip through the shepherd-checkpoint marker', () => {
  const body = renderCheckpoint(checkpoint)
  assert.ok(body.includes(CHECKPOINT_MARKER))
  assert.deepEqual(parseCheckpoint(body), checkpoint)
})

test('parseCheckpoint returns undefined for a comment body without the marker', () => {
  assert.equal(parseCheckpoint('just a regular PR comment'), undefined)
})

test('validateCheckpoint accepts a well-formed checkpoint object and rejects a malformed one', () => {
  assert.deepEqual(validateCheckpoint(checkpoint), checkpoint)
  assert.equal(validateCheckpoint({ ...checkpoint, sessionId: 'not-a-session-id' }), undefined)
})

test('sortedCheckpointCandidates extracts checkpoints from a GitHub comment list', () => {
  const body = renderCheckpoint(checkpoint)
  const comments = [
    { id: 1, body: 'unrelated comment', created_at: '2026-09-01T00:00:00.000Z' },
    { id: 2, body, created_at: '2026-09-01T00:01:00.000Z' },
  ]
  const candidates = sortedCheckpointCandidates(comments)
  assert.equal(candidates.length, 1)
  assert.equal(candidates[0].comment.id, 2)
  assert.deepEqual(candidates[0].checkpoint, checkpoint)
})

test('isTrustedCheckpointComment is re-exported from vouchington-tooling/gha-pr-checkpoint', () => {
  assert.equal(typeof isTrustedCheckpointComment, 'function')
  assert.equal(
    isTrustedCheckpointComment(
      {
        id: 1,
        user: { login: 'github-actions[bot]', type: 'Bot' },
        performed_via_github_app: { slug: 'github-actions' },
      },
      { actor: 'github-actions[bot]', appSlug: 'github-actions' },
    ),
    true,
  )
})

test('isTrustedCheckpointComment rejects a comment whose author does not match the actor', () => {
  assert.equal(
    isTrustedCheckpointComment(
      {
        id: 1,
        user: { login: 'someone-else', type: 'Bot' },
        performed_via_github_app: { slug: 'github-actions' },
      },
      { actor: 'github-actions[bot]', appSlug: 'github-actions' },
    ),
    false,
  )
})
