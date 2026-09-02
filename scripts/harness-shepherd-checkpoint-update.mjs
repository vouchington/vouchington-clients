import { updateExactCheckpoint as updatePublished } from 'vouchington-tooling/gha-pr-checkpoint'
import { CODEC } from './harness-shepherd-checkpoint.mjs'

export function updateExactCheckpoint(comment, context, status, session) {
  return updatePublished(comment, context, status, session, CODEC)
}
