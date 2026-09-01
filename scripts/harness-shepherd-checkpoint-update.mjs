import {
  isTrustedCheckpointComment,
  parseCheckpoint,
  renderCheckpoint,
} from './harness-shepherd-checkpoint.mjs'

export function updateExactCheckpoint(comment, context, status, session) {
  const checkpoint = parseCheckpoint(comment.body ?? '')
  if (
    !checkpoint ||
    comment.id !== context.commentId ||
    !isTrustedCheckpointComment(comment, context) ||
    checkpoint.actor !== context.actor ||
    checkpoint.repository !== context.repository ||
    checkpoint.pr !== context.pr ||
    checkpoint.triggerCommentId !== context.triggerCommentId ||
    checkpoint.runId !== context.runId ||
    checkpoint.headRef !== context.headRef ||
    checkpoint.startSha !== context.headSha
  ) {
    throw new Error('Checkpoint comment does not match the active shepherd binding')
  }
  if (status === 'running' && (!session.id || !session.url)) {
    throw new Error('Running checkpoint requires a Harness session id and URL')
  }
  const next = {
    ...checkpoint,
    ...(session.id ? { sessionId: session.id } : {}),
    ...(session.url ? { sessionUrl: session.url } : {}),
    status,
    updatedAt: new Date().toISOString(),
  }
  return renderCheckpoint(next)
}
