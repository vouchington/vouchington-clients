import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { parseInteger, requiredEnvironmentValue } from 'auto-harness-client/actions'

import { renderCheckpoint } from './harness-shepherd-checkpoint.mjs'
import { selectResumeCheckpoint } from './harness-shepherd-checkpoint-resume.mjs'
import { updateExactCheckpoint } from './harness-shepherd-checkpoint-update.mjs'

const USAGE = 'Usage: harness-shepherd-checkpoint-cli.mjs render|select|update <path>'

export function runCheckpointCli(environment = process.env, argv = process.argv) {
  const command = argv[2]
  if (command === 'render') {
    const checkpoint = JSON.parse(readFileSync(argv[3] ?? '', 'utf8'))
    return renderCheckpoint(checkpoint)
  }
  if (command === 'update') {
    const comment = JSON.parse(readFileSync(argv[3] ?? '', 'utf8'))
    const status = requiredEnvironmentValue(environment, 'CHECKPOINT_STATUS')
    if (status !== 'running' && status !== 'failed') {
      throw new Error('CHECKPOINT_STATUS is invalid')
    }
    return updateExactCheckpoint(
      comment,
      {
        actor: requiredEnvironmentValue(environment, 'CHECKPOINT_ACTOR'),
        commentId: parseInteger(
          requiredEnvironmentValue(environment, 'CHECKPOINT_COMMENT_ID'),
          'CHECKPOINT_COMMENT_ID',
          1,
        ),
        headRef: requiredEnvironmentValue(environment, 'PR_HEAD_REF'),
        headSha: requiredEnvironmentValue(environment, 'PR_HEAD_SHA'),
        pr: parseInteger(requiredEnvironmentValue(environment, 'PR_NUMBER'), 'PR_NUMBER', 1),
        repository: requiredEnvironmentValue(environment, 'GITHUB_REPOSITORY'),
        runId: requiredEnvironmentValue(environment, 'GITHUB_RUN_ID'),
        triggerCommentId: parseInteger(
          requiredEnvironmentValue(environment, 'TRIGGER_COMMENT_ID'),
          'TRIGGER_COMMENT_ID',
          1,
        ),
      },
      status,
      { id: environment.HARNESS_SESSION_ID, url: environment.HARNESS_SESSION_URL },
    )
  }
  if (command !== 'select') throw new Error(USAGE)
  const comments = JSON.parse(readFileSync(argv[3] ?? '', 'utf8'))
  const repository = requiredEnvironmentValue(environment, 'GITHUB_REPOSITORY')
  const result = selectResumeCheckpoint(comments, {
    repository,
    pr: parseInteger(requiredEnvironmentValue(environment, 'PR_NUMBER'), 'PR_NUMBER', 1),
    headRef: requiredEnvironmentValue(environment, 'PR_HEAD_REF'),
    headSha: requiredEnvironmentValue(environment, 'PR_HEAD_SHA'),
    actor: requiredEnvironmentValue(environment, 'CHECKPOINT_ACTOR'),
    isAncestor(candidate, head) {
      try {
        const status = execFileSync(
          'gh',
          ['api', `repos/${repository}/compare/${candidate}...${head}`, '--jq', '.status'],
          { encoding: 'utf8' },
        ).trim()
        return status === 'ahead' || status === 'identical'
      } catch {
        return false
      }
    },
    isShepherdRun(runId) {
      try {
        const verifiedId = execFileSync(
          'gh',
          [
            'api',
            `repos/${repository}/actions/runs/${runId}`,
            '--jq',
            'select(.name == "Automation Shepherd" and .event == "issue_comment" and .path == ".github/workflows/shepherd.yml") | .id',
          ],
          { encoding: 'utf8' },
        ).trim()
        return verifiedId === runId
      } catch {
        return false
      }
    },
  })
  return result
    ? JSON.stringify({
        sessionId: result.checkpoint.sessionId,
        commentId: result.commentId,
        startSha: result.checkpoint.startSha,
        sessionStartSha: result.checkpoint.sessionStartSha,
        runId: result.checkpoint.runId,
      })
    : '{}'
}

if (import.meta.main) {
  try {
    process.stdout.write(runCheckpointCli())
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`)
    process.exitCode = 1
  }
}
