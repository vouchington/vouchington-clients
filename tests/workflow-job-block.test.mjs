import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { jobBlock, stepBlock } from './workflow-job-block.mjs'

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

  it('ignores indented step text that looks like a job key', () => {
    const block = jobBlock(
      'jobs:\n  build:\n    steps:\n      - run: |\n          echo\n          verify: nested\n  verify:\n    runs-on: ubuntu-slim\n',
      'verify',
    )

    assert.equal(block, '  verify:\n    runs-on: ubuntu-slim\n')
  })

  it('ignores a job-like key above the jobs section', () => {
    const block = jobBlock(
      'env:\n  verify: no\njobs:\n  verify:\n    runs-on: ubuntu-slim\n',
      'verify',
    )

    assert.equal(block, '  verify:\n    runs-on: ubuntu-slim\n')
  })

  it('reports a missing job with the caller message', () => {
    assert.throws(() => jobBlock(workflow, 'missing', 'missing workflow job missing'), {
      message: 'missing workflow job missing',
    })
  })
})

const steps = `jobs:
  build:
    steps:
      - name: First
        run: echo first
      - name: First extended
        run: echo longer
      - name: Run
        run: |
          echo '      - name: nested'
      - name: Last
        run: echo last
`

describe('workflow step block', () => {
  it('returns one step through the line before the next step', () => {
    const block = stepBlock(steps, 'First')

    assert.equal(block, '      - name: First\n        run: echo first')
    assert.doesNotMatch(block, /First extended/u)
  })

  it('keeps a longer step name distinct from its prefix', () => {
    assert.throws(() => stepBlock(steps, 'Fir', 'missing workflow step Fir'), {
      message: 'missing workflow step Fir',
    })
    assert.match(stepBlock(steps, 'First extended'), /echo longer/u)
  })

  it('keeps a name-like string inside a script with the step', () => {
    const block = stepBlock(steps, 'Run')

    assert.match(block, /echo '      - name: nested'/u)
    assert.doesNotMatch(block, /echo last/u)
  })

  it('returns the last step through the end of the source', () => {
    const block = stepBlock(steps, 'Last')

    assert.equal(block, '      - name: Last\n        run: echo last\n')
  })

  it('slices a composite action step at four-space indent', () => {
    const action = [
      '    - name: Checkout',
      '      uses: actions/checkout',
      '    - name: Test',
      '      run: npm test',
      '',
    ].join('\n')

    assert.equal(
      stepBlock(action, 'Checkout', 'missing step Checkout', 4),
      '    - name: Checkout\n      uses: actions/checkout',
    )
  })
})
