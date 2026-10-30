import assert from 'node:assert/strict'
import test from 'node:test'
import { overlaps } from '../scripts/paired-exporter-paths.mjs'
import { within } from '../scripts/path-within.mjs'

test('overlaps accepts the same directory and strict descendants', () => {
  assert.equal(overlaps('/root', '/root'), true)
  assert.equal(within('/root', '/root'), false)
  assert.equal(overlaps('/root', '/root/child/file'), true)
  assert.equal(overlaps('/root/child', '/root'), false)
  assert.equal(overlaps('/root/a', '/root/b'), false)
  assert.equal(overlaps('/root', '/root/../other'), false)
  assert.equal(overlaps('/root/', '/root'), true)
})
