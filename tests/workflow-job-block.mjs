import assert from 'node:assert/strict'

const escapeRegExp = text => text.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')

// Slice one workflow job from its two-space header at a line start, after the
// top-level `jobs:` key, to the next two-space key. Job bodies indent further.
export function jobBlock(workflow, job, missingMessage = `missing job ${job}`) {
  const jobsStart = workflow.search(/^jobs:/mu)
  const scope = jobsStart === -1 ? 0 : jobsStart
  const header = new RegExp(`^ {2}${escapeRegExp(job)}:`, 'mu').exec(workflow.slice(scope))
  assert.notEqual(header, null, missingMessage)
  const remainder = workflow.slice(scope + header.index)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

// Slice one Actions step from its exact `- name:` line through the line before
// the next step at the same indent. A longer step name is not a match.
export function stepBlock(source, name, missingMessage = `missing step ${name}`, indent = 6) {
  const prefix = ' '.repeat(indent)
  const marker = `${prefix}- name: ${name}\n`
  const start = source.indexOf(marker)
  assert.notEqual(start, -1, missingMessage)
  const remainder = source.slice(start)
  const nextStep = remainder.search(new RegExp(`\\n${prefix}- name:`, 'u'))
  return remainder.slice(0, nextStep === -1 ? remainder.length : nextStep)
}
