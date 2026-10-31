import assert from 'node:assert/strict'
import test from 'node:test'
import { within } from '../scripts/path-within.mjs'

test('within accepts only strict descendants', () => {
  assert.equal(within('/root', '/root/child/file'), true)
  assert.equal(within('/root', '/root'), false)
  assert.equal(within('/root', '/root/..'), false)
  assert.equal(within('/root', '/other'), false)
  assert.equal(within('/root/child', '/root'), false)
})
