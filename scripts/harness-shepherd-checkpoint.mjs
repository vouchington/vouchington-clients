import {
  parseCheckpoint as parsePublished,
  renderCheckpoint as renderPublished,
  sortedCheckpointCandidates as sortedPublished,
  validateCheckpoint as validatePublished,
} from 'vouchington-tooling/gha-pr-checkpoint'

export { isTrustedCheckpointComment } from 'vouchington-tooling/gha-pr-checkpoint'

export const CHECKPOINT_MARKER = 'shepherd-checkpoint:v1'

/** Auto Harness production session id: `sess-` plus four random bytes as lowercase hex. */
export const HARNESS_SESSION_ID = /^sess-[0-9a-f]{8}$/u

const CODEC = {
  marker: CHECKPOINT_MARKER,
  sessionIdPattern: HARNESS_SESSION_ID,
}

export function renderCheckpoint(checkpoint) {
  return renderPublished(checkpoint, { marker: CHECKPOINT_MARKER })
}

export function parseCheckpoint(body) {
  return parsePublished(body, CODEC)
}

export function validateCheckpoint(value) {
  return validatePublished(value, CODEC)
}

export function sortedCheckpointCandidates(comments) {
  return sortedPublished(comments, CODEC)
}
