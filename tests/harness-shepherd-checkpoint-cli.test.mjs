import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { CHECKPOINT_MARKER, parseCheckpoint } from '../scripts/harness-shepherd-checkpoint.mjs'
import { runCheckpointCli } from '../scripts/harness-shepherd-checkpoint-cli.mjs'

const startSha = 'a'.repeat(40)

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
    sessionId: 'sess-0123abcd',
    resumeSourceRunId: '',
    status: 'queued',
    createdAt: '2026-07-26T05:35:55Z',
    updatedAt: '2026-07-26T05:35:55Z',
    ...overrides,
  }
}

function withTempFile(content, run) {
  const dir = mkdtempSync(join(tmpdir(), 'harness-shepherd-checkpoint-cli-'))
  const path = join(dir, 'input.json')
  writeFileSync(path, JSON.stringify(content))
  try {
    return run(path)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('prints concise CLI failures without a raw stack trace', () => {
  const result = spawnSync(process.execPath, ['scripts/harness-shepherd-checkpoint-cli.mjs'], {
    encoding: 'utf8',
  })
  assert.equal(result.status, 1)
  assert.equal(
    result.stderr.trim(),
    'Usage: harness-shepherd-checkpoint-cli.mjs render|select|update <path>',
  )
})

test('render subcommand writes the checkpoint body for the given file', () => {
  withTempFile(checkpoint(), path => {
    const body = runCheckpointCli({}, ['node', 'cli', 'render', path])
    assert.equal(parseCheckpoint(body).sessionId, 'sess-0123abcd')
  })
})

test('update subcommand rejects a comment that does not match the active binding', () => {
  const comment = {
    id: 42,
    user: { login: 'github-actions[bot]', type: 'Bot' },
    performed_via_github_app: { slug: 'github-actions' },
    body: `<!-- ${CHECKPOINT_MARKER} ${JSON.stringify(checkpoint())} -->`,
  }
  withTempFile(comment, path => {
    assert.throws(
      () =>
        runCheckpointCli(
          {
            CHECKPOINT_STATUS: 'failed',
            CHECKPOINT_ACTOR: 'github-actions[bot]',
            CHECKPOINT_COMMENT_ID: '1',
            PR_HEAD_REF: 'codex/fix',
            PR_HEAD_SHA: startSha,
            PR_NUMBER: '8592',
            GITHUB_REPOSITORY: 'vouchington/vouchington-clients',
            GITHUB_RUN_ID: '30189230576',
            TRIGGER_COMMENT_ID: '100',
          },
          ['node', 'cli', 'update', path],
        ),
      /Checkpoint comment/u,
    )
  })
})

test('update subcommand rejects an invalid CHECKPOINT_STATUS', () => {
  withTempFile({ id: 1, body: '' }, path => {
    assert.throws(
      () =>
        runCheckpointCli(
          {
            CHECKPOINT_STATUS: 'complete',
            CHECKPOINT_ACTOR: 'a',
            CHECKPOINT_COMMENT_ID: '1',
            PR_HEAD_REF: 'b',
            PR_HEAD_SHA: 'c',
            PR_NUMBER: '1',
            GITHUB_REPOSITORY: 'r',
            GITHUB_RUN_ID: '1',
            TRIGGER_COMMENT_ID: '1',
          },
          ['node', 'cli', 'update', path],
        ),
      /CHECKPOINT_STATUS is invalid/u,
    )
  })
})

test('select subcommand returns an empty object when no candidate resolves', () => {
  withTempFile([], path => {
    const result = runCheckpointCli(
      {
        GITHUB_REPOSITORY: 'vouchington/vouchington-clients',
        PR_NUMBER: '8592',
        PR_HEAD_REF: 'codex/fix',
        PR_HEAD_SHA: startSha,
        CHECKPOINT_ACTOR: 'github-actions[bot]',
      },
      ['node', 'cli', 'select', path],
    )
    assert.equal(result, '{}')
  })
})
