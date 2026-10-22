import assert from 'node:assert/strict'
import { resolve } from 'node:path'
import test from 'node:test'

import { loadRules, matchRule } from 'coverage-check/dist/src/rules.mjs'

const rules = loadRules(resolve(import.meta.dirname, '../.coverage-rules.yml'))
const admission =
  'swift-clients/ui/Tests/VouchaUITests/NativeChatFoundationModelsPinnedSDKAdmissionTests.swift'

test('the Apple Intelligence measurement is exempt and neighboring Swift tests stay at 90%', () => {
  assert.equal(matchRule(admission, rules)?.patch_coverage_min, 0)
  assert.equal(
    matchRule(
      'swift-clients/ui/Tests/VouchaUITests/NativeChatFoundationModelsAdmissionTests.swift',
      rules,
    )?.patch_coverage_min,
    90,
  )
  assert.equal(
    matchRule('swift-clients/core/Tests/VouchaIntegrationTests/VouchaIntegrationTests.swift', rules)
      ?.patch_coverage_min,
    0,
  )
  assert.equal(
    matchRule('swift-clients/core/Sources/VouchaCore/Example.swift', rules)?.patch_coverage_min,
    90,
  )
})
