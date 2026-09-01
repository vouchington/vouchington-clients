import {
  isTrustedCheckpointComment,
  sortedCheckpointCandidates,
} from './harness-shepherd-checkpoint.mjs'

export function selectResumeCheckpoint(comments, context) {
  const candidates = sortedCheckpointCandidates(comments)
  for (const { comment, checkpoint } of candidates) {
    if (
      !isTrustedCheckpointComment(comment, context) ||
      checkpoint.actor !== context.actor ||
      checkpoint.repository !== context.repository ||
      checkpoint.pr !== context.pr ||
      checkpoint.headRef !== context.headRef ||
      !checkpoint.sessionId ||
      !context.isShepherdRun(checkpoint.runId) ||
      !context.isAncestor(checkpoint.startSha, context.headSha) ||
      !context.isAncestor(checkpoint.sessionStartSha, context.headSha)
    ) {
      continue
    }
    if (checkpoint.status === 'complete' || checkpoint.status === 'unresumable') return undefined
    return { checkpoint, commentId: comment.id }
  }
  return undefined
}
