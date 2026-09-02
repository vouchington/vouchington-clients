import { selectResumeCheckpoint as selectPublished } from 'vouchington-tooling/gha-pr-checkpoint'
import { CODEC } from './harness-shepherd-checkpoint.mjs'

export function selectResumeCheckpoint(comments, context) {
  return selectPublished(comments, context, CODEC)
}
