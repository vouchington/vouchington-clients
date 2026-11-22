import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { jobBlock, stepBlock } from './workflow-job-block.mjs'

// Boot to ready took p50 178 s, p95 285 s, max 454 s over 1217 passing jobs. A 4-minute wait that
// began after the artifact download killed run 45006025242 at 400 s after boot, so the deadline is
// 600 s from the boot command with an 11-minute step cap behind it.
const workflow = readFileSync(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)

describe('iOS Simulator boot deadline', () => {
  for (const [id, wait, prefix] of [
    ['build-ios-app', 'Wait for iPhone Simulator readiness', 'ios-launch'],
    ['build-ios-ipad', 'Wait for iPad Simulator readiness', 'ipad-launch'],
  ]) {
    it(`anchors the ${id} readiness wait to the boot command`, () => {
      const job = jobBlock(workflow, id, `missing workflow job ${id}`)
      const step = stepBlock(job, wait)
      assert.match(step, /\n        timeout-minutes: 11\n/u)
      assert.ok(step.includes(`cat "$RUNNER_TEMP/${prefix}-boot-epoch"`))
      assert.match(step, /remaining=\$\(\(600 - /u)
      assert.match(step, /perl -e 'alarm shift; exec @ARGV' "\$remaining" xcrun simctl bootstatus/u)
      assert.ok(job.includes(`date +%s > "$RUNNER_TEMP/${prefix}-boot-epoch"`))
    })
  }
})
