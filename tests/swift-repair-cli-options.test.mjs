import assert from 'node:assert/strict'
import { it } from 'node:test'
import { parseCli } from '../scripts/swift-repair-common.mjs'

it('preserves Swift repair option values and ignores positional arguments', () => {
  assert.deepEqual(
    parseCli(['pull-request', '--metadata', 'metadata.json', '--raw-diff', 'changes.raw']),
    { metadata: 'metadata.json', 'raw-diff': 'changes.raw' },
  )
})

it('preserves adjacent flags, repeated options, and missing trailing values', () => {
  assert.deepEqual(
    parseCli(['--verbose', '--input', 'first.json', '--input', 'second.json', '--output']),
    { verbose: true, input: 'second.json', output: undefined },
  )
})
