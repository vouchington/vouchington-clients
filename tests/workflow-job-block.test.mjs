import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const workflow = `name: example
on:
  push:
    branches: [main]
jobs:
  first:
    runs-on: ubuntu-slim
    steps:
      - run: echo first
  second:
    runs-on: ubuntu-latest
    steps:
      - run: echo second
`

describe('workflow job block', () => {
  it('returns one job through the line before the next job', () => {
    const block = jobBlock(workflow, 'first')

    assert.match(block, /^ {2}first:\n/u)
    assert.match(block, /echo first/u)
    assert.match(block, /runs-on: ubuntu-slim/u)
    assert.doesNotMatch(block, /second:/u)
    assert.doesNotMatch(block, /echo second/u)
  })

  it('returns the last job through the end of the workflow', () => {
    const block = jobBlock(workflow, 'second')

    assert.match(block, /^ {2}second:\n/u)
    assert.match(block, /echo second/u)
    assert.doesNotMatch(block, /echo first/u)
  })

  it('stops at a two-space key that carries a same-line value', () => {
    const block = jobBlock(
      'jobs:\n  alpha:\n    runs-on: ubuntu-slim\n  timeout-minutes: 5\n  beta:\n    runs-on: ubuntu-latest\n',
      'alpha',
    )

    assert.equal(block, '  alpha:\n    runs-on: ubuntu-slim')
  })

  it('reports a missing job with the caller message', () => {
    assert.throws(() => jobBlock(workflow, 'missing', 'missing workflow job missing'), {
      message: 'missing workflow job missing',
    })
  })
})
