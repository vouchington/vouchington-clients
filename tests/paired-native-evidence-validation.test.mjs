import assert from 'node:assert/strict'
import { it } from 'node:test'
import { bindArtifact } from '../scripts/paired-native-test-evidence.mjs'
import { validateInstrumentationResult } from '../scripts/paired-native-test-runtime-result.mjs'
import { validateCleanupShape } from '../scripts/paired-native-cleanup-evidence.mjs'

it('rejects non-object and mismatched evidence schemas with consistent diagnostics', () => {
  for (const [validate, label] of [
    [expected => bindArtifact({ expected }), 'expected artifact'],
    [validateInstrumentationResult, 'runtime result'],
    [validateCleanupShape, 'cleanup evidence'],
  ]) {
    for (const value of [undefined, null, [], 'evidence', 1]) {
      assert.throws(() => validate(value), {
        message: `Paired native test evidence rejected: ${label} must be an object`,
      })
    }
    for (const value of [{}, { unexpected: true }]) {
      assert.throws(() => validate(value), {
        message: `Paired native test evidence rejected: ${label} fields differ from the fixed schema`,
      })
    }
  }
})

it('rejects extra, missing, and replaced artifact metadata fields', () => {
  const digest = 'a'.repeat(64)
  const expected = { name: 'paired-native-apks-1-1', digest, id: 42 }
  for (const invalid of [
    { ...expected, extra: true },
    { name: expected.name, digest },
    { name: expected.name, digest, artifactId: expected.id },
  ]) {
    assert.throws(() => bindArtifact({ expected: invalid }), {
      message:
        'Paired native test evidence rejected: expected artifact fields differ from the fixed schema',
    })
  }
})
