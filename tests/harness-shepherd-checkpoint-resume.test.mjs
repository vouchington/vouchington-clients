import assert from 'node:assert/strict'
import test from 'node:test'

import { CHECKPOINT_MARKER, renderCheckpoint } from '../scripts/harness-shepherd-checkpoint.mjs'
import { selectResumeCheckpoint } from '../scripts/harness-shepherd-checkpoint-resume.mjs'

const startSha = 'a'.repeat(40)
const headSha = 'b'.repeat(40)
const sessionId = 'sess-0123abcd'

function checkpoint(overrides = {}) {
  return {
    marker: CHECKPOINT_MARKER,
    repository: 'vouchington/vouchington-clients',
    pr: 8592,
    headRef: 'codex/fix',
    startSha,
    sessionStartSha: startSha,
    runId: '30189230576',
    runUrl: 'https://github.com/vouchington/vouchington-clients/actions/runs/30189230576',
    actor: 'github-actions[bot]',
    sessionId,
    resumeSourceRunId: '',
    status: 'failed',
    createdAt: '2026-07-26T05:35:55Z',
    updatedAt: '2026-07-26T05:35:55Z',
    ...overrides,
  }
}

function context(overrides = {}) {
  return {
    repository: 'vouchington/vouchington-clients',
    pr: 8592,
    headRef: 'codex/fix',
    headSha,
    actor: 'github-actions[bot]',
    isAncestor: candidate => candidate === startSha,
    isShepherdRun: runId => runId === '30189230576',
    ...overrides,
  }
}

function trustedBot() {
  return {
    user: { login: 'github-actions[bot]', type: 'Bot' },
    performed_via_github_app: { slug: 'github-actions' },
  }
}

test('accepts a trusted remote Harness session without a local rollout', () => {
  const result = selectResumeCheckpoint(
    [
      {
        id: 42,
        ...trustedBot(),
        body: renderCheckpoint(checkpoint()),
        created_at: '2026-07-26T05:35:55Z',
      },
    ],
    context(),
  )
  assert.deepEqual(result, { checkpoint: checkpoint(), commentId: 42 })
})

test('keeps an awaiting-verification checkpoint resumable', () => {
  const awaiting = checkpoint({ status: 'awaiting_verification' })
  const result = selectResumeCheckpoint(
    [
      {
        id: 42,
        ...trustedBot(),
        body: renderCheckpoint(awaiting),
        created_at: '2026-07-26T05:35:55Z',
      },
    ],
    context(),
  )
  assert.equal(result.commentId, 42)
  assert.equal(result.checkpoint.status, 'awaiting_verification')
})

test('treats a completed or unresumable checkpoint as unresumable', () => {
  for (const status of ['complete', 'unresumable']) {
    const result = selectResumeCheckpoint(
      [
        {
          id: 42,
          ...trustedBot(),
          body: renderCheckpoint(checkpoint({ status })),
          created_at: '2026-07-26T05:35:55Z',
        },
      ],
      context(),
    )
    assert.equal(result, undefined)
  }
})

test('rejects forged, mismatched, and divergent checkpoints', () => {
  const candidates = [
    { value: checkpoint(), user: { login: 'human', type: 'User' }, performed_via_github_app: null },
    {
      value: checkpoint(),
      user: { login: 'github-actions[bot]', type: 'Bot' },
      performed_via_github_app: null,
    },
    { value: checkpoint({ repository: 'other/repo' }), ...trustedBot() },
    { value: checkpoint({ startSha: 'c'.repeat(40) }), ...trustedBot() },
    { value: checkpoint({ runId: '999' }), ...trustedBot() },
  ]
  for (const [index, candidate] of candidates.entries()) {
    const result = selectResumeCheckpoint(
      [
        {
          id: index,
          user: candidate.user,
          performed_via_github_app: candidate.performed_via_github_app,
          body: renderCheckpoint(candidate.value),
        },
      ],
      context(),
    )
    assert.equal(result, undefined)
  }
})

test('rejects a session whose original start is not an ancestor of the current head', () => {
  const result = selectResumeCheckpoint(
    [
      {
        id: 1,
        ...trustedBot(),
        body: renderCheckpoint(checkpoint({ sessionStartSha: 'c'.repeat(40) })),
      },
    ],
    context(),
  )
  assert.equal(result, undefined)
})

test('preserves the original trusted session start across descendant-head resumptions', () => {
  const resumedCheckpoint = checkpoint({
    startSha: headSha,
    sessionStartSha: startSha,
    resumeSourceRunId: '30180000000',
  })
  const result = selectResumeCheckpoint(
    [{ id: 43, ...trustedBot(), body: renderCheckpoint(resumedCheckpoint) }],
    context({ isAncestor: candidate => candidate === headSha || candidate === startSha }),
  )
  assert.equal(result.checkpoint.startSha, headSha)
  assert.equal(result.checkpoint.sessionStartSha, startSha)
  assert.equal(result.checkpoint.resumeSourceRunId, '30180000000')
})

test('returns undefined when no candidate comments contain a checkpoint', () => {
  assert.equal(selectResumeCheckpoint([{ id: 1, body: 'just a comment' }], context()), undefined)
})
