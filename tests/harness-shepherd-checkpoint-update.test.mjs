import assert from 'node:assert/strict'
import test from 'node:test'

import {
  CHECKPOINT_MARKER,
  parseCheckpoint,
  renderCheckpoint,
} from '../scripts/harness-shepherd-checkpoint.mjs'
import { updateExactCheckpoint } from '../scripts/harness-shepherd-checkpoint-update.mjs'

const startSha = 'a'.repeat(40)
const sessionId = 'sess-0123abcd'

function checkpoint(overrides = {}) {
  return {
    marker: CHECKPOINT_MARKER,
    repository: 'vouchington/vouchington-clients',
    pr: 8592,
    triggerCommentId: 100,
    headRef: 'codex/fix',
    startSha,
    sessionStartSha: startSha,
    runId: '30189230576',
    runUrl: 'https://github.com/vouchington/vouchington-clients/actions/runs/30189230576',
    actor: 'github-actions[bot]',
    sessionId,
    resumeSourceRunId: '',
    status: 'queued',
    createdAt: '2026-07-26T05:35:55Z',
    updatedAt: '2026-07-26T05:35:55Z',
    ...overrides,
  }
}

function trustedComment(overrides = {}) {
  return {
    id: 42,
    user: { login: 'github-actions[bot]', type: 'Bot' },
    performed_via_github_app: { slug: 'github-actions' },
    body: renderCheckpoint(checkpoint()),
    ...overrides,
  }
}

function context(overrides = {}) {
  return {
    actor: 'github-actions[bot]',
    commentId: 42,
    headRef: 'codex/fix',
    headSha: startSha,
    pr: 8592,
    repository: 'vouchington/vouchington-clients',
    runId: '30189230576',
    triggerCommentId: 100,
    ...overrides,
  }
}

test('updates the exact trusted checkpoint binding with a session URL', () => {
  const rendered = updateExactCheckpoint(trustedComment(), context(), 'running', {
    id: sessionId,
    url: `https://harness.example.com/sessions/${sessionId}`,
  })
  const updated = parseCheckpoint(rendered)
  assert.equal(updated.status, 'running')
  assert.equal(updated.sessionId, sessionId)
  assert.ok(updated.sessionUrl.includes(sessionId))
})

test('rejects a comment whose binding no longer matches the active context', () => {
  const comment = trustedComment()
  for (const override of [
    { commentId: 43 },
    { headSha: 'c'.repeat(40) },
    { pr: 1 },
    { runId: '1' },
  ]) {
    assert.throws(
      () => updateExactCheckpoint(comment, context(override), 'failed', {}),
      /Checkpoint comment/u,
    )
  }
})

test('rejects an untrusted comment author even with a matching body', () => {
  const comment = trustedComment({ user: { login: 'someone-else', type: 'Bot' } })
  assert.throws(
    () => updateExactCheckpoint(comment, context(), 'failed', {}),
    /Checkpoint comment/u,
  )
})

test('rejects a comment without a parseable checkpoint body', () => {
  const comment = trustedComment({ body: 'not a checkpoint' })
  assert.throws(
    () => updateExactCheckpoint(comment, context(), 'failed', {}),
    /Checkpoint comment/u,
  )
})

test('requires a session id and URL to transition to running', () => {
  assert.throws(
    () => updateExactCheckpoint(trustedComment(), context(), 'running', {}),
    /Running checkpoint requires/u,
  )
})

test('allows transitioning to failed without a session', () => {
  const rendered = updateExactCheckpoint(trustedComment(), context(), 'failed', {})
  assert.equal(parseCheckpoint(rendered).status, 'failed')
})
