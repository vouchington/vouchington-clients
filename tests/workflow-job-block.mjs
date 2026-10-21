import assert from 'node:assert/strict'

// Slice one workflow job at the next two-space key. Job bodies indent further.
export function jobBlock(workflow, job, missingMessage = `missing job ${job}`) {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, missingMessage)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}
