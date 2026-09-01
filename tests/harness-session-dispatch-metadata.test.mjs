import assert from 'node:assert/strict'
import test from 'node:test'

import { HarnessDispatchError } from 'auto-harness-client/actions'

import { buildHarnessDispatchMetadata } from '../scripts/harness-session-dispatch-metadata.mjs'

const baseEnv = {
  AGENT_REF: 'main',
  EXPECTED_HEAD_SHA: 'a'.repeat(40),
  COMPLETION_MODE: 'pr',
  ISSUE_NUMBER: '',
  TRIGGER_COMMENT_ID: '',
  PUBLISH_CONTRACT: 'none',
  PUBLISH_BASE_REF: '',
  PUBLISH_TARGET_PR_NUMBER: '',
  PUBLISH_TITLE_PREFIX: '',
  PUBLISH_TITLE_SUFFIX: '',
  PUBLISH_DUPLICATE_KEY: '',
  EXISTING_ISSUE_MAINTENANCE: 'false',
  ALLOW_DUPLICATE_ISSUE_COMPLETION: 'false',
  PR_LABEL: '',
  PR_SHEPHERD_VERSION: '',
  CHECKPOINT_ISSUE_NUMBER: '',
  SLACK_SOURCE: '',
  SOURCE_RUN_ID: '',
  SOURCE_RUN_ATTEMPT: '',
  SOURCE_RUN_CONCLUSION: '',
  PUBLISH_RELATED_CANDIDATES: '[]',
}

test('forwards required fields, keeps false booleans, and drops unset optional ones', () => {
  assert.deepEqual(buildHarnessDispatchMetadata(baseEnv), {
    agentRef: 'main',
    expectedHeadSha: 'a'.repeat(40),
    completionMode: 'pr',
    publishContract: 'none',
    existingIssueMaintenance: 'false',
    allowDuplicateIssueCompletion: 'false',
  })
})

test('omits publish-related-candidates when empty', () => {
  const result = buildHarnessDispatchMetadata({ ...baseEnv, PUBLISH_RELATED_CANDIDATES: '[]' })
  assert.equal('publishRelatedCandidates' in result, false)
})

test('serializes non-empty publish-related-candidates to a metadata string', () => {
  const result = buildHarnessDispatchMetadata({
    ...baseEnv,
    PUBLISH_RELATED_CANDIDATES: '["#123","#456"]',
  })
  assert.equal(result.publishRelatedCandidates, '["#123","#456"]')
  assert.equal(typeof result.publishRelatedCandidates, 'string')
  assert.deepEqual(JSON.parse(result.publishRelatedCandidates ?? ''), ['#123', '#456'])
})

for (const padLength of [600, 5000]) {
  test(`drops a publish-related-candidates payload of ${padLength} bytes instead of failing the step (no hard ceiling)`, () => {
    const oversized = JSON.stringify(['x'.repeat(padLength)])
    const originalWrite = process.stderr.write.bind(process.stderr)
    const calls = []
    process.stderr.write = chunk => {
      calls.push(chunk)
      return true
    }
    try {
      const result = buildHarnessDispatchMetadata({
        ...baseEnv,
        PUBLISH_RELATED_CANDIDATES: oversized,
      })
      assert.equal('publishRelatedCandidates' in result, false)
      assert.ok(calls.some(chunk => String(chunk).includes('publish-related-candidates dropped')))
      assert.ok(
        calls.some(chunk => String(chunk).includes('bytes exceeds the 512-byte non-gating limit')),
      )
    } finally {
      process.stderr.write = originalWrite
    }
  })
}

test('rejects a metadata field over the 512-character bound', () => {
  assert.throws(
    () => buildHarnessDispatchMetadata({ ...baseEnv, PR_LABEL: 'x'.repeat(513) }),
    HarnessDispatchError,
  )
  assert.throws(
    () => buildHarnessDispatchMetadata({ ...baseEnv, PR_LABEL: 'x'.repeat(513) }),
    /prLabel exceeds 512 characters/,
  )
})

test('rejects publish-related-candidates that is not a JSON array', () => {
  assert.throws(
    () => buildHarnessDispatchMetadata({ ...baseEnv, PUBLISH_RELATED_CANDIDATES: '{}' }),
    /publish-related-candidates must be a JSON array/,
  )
})
