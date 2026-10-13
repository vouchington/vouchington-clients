import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'

const jobBlock = (workflow, job) => {
  const start = workflow.indexOf(`  ${job}:`)
  assert.notEqual(start, -1, `missing workflow job ${job}`)
  const remainder = workflow.slice(start)
  const nextJob = remainder.search(/\n {2}[A-Za-z0-9_-]+:/u)
  return remainder.slice(0, nextJob === -1 ? remainder.length : nextJob)
}

describe('iOS Simulator app compile fan-in', () => {
  it('requires the iOS build to succeed and rejects failure or skip', () => {
    const workflow = readFileSync(
      new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
      'utf8',
    )
    const testsJob = jobBlock(workflow, 'tests')
    const needs = testsJob.match(/\n    needs:\n((?:      - [^\n]+\n)+)/u)?.[1] ?? ''
    assert.match(needs, /^      - build-ios-app$/mu)

    const expression = workflow
      .slice(workflow.lastIndexOf('jq -e --arg route'))
      .split("'\n")[1]
      .split("\n          '")[0]
    const baseNeeds = Object.fromEntries(
      [
        'produce',
        'verify',
        'dotnet-portable',
        'periphery-swift-core',
        'periphery-swift-ui',
        'test-swift-core-linux',
        'test-swift-core',
        'test-swift-android',
        'test-swift-ui',
        'swift-patch-coverage',
        'build-android-core',
        'build-macos-app',
        'dotnet-maui',
        'dotnet-windows-image-preview',
        'dotnet-windows-maui',
      ].map(job => [job, { result: 'success' }]),
    )
    baseNeeds['dotnet-windows-image-preview'] = { result: 'skipped' }
    baseNeeds['dotnet-windows-maui'] = { result: 'skipped' }

    for (const [result, succeeds] of [
      ['success', true],
      ['failure', false],
      ['skipped', false],
    ]) {
      const needs = { ...baseNeeds, 'build-ios-app': { result } }
      let accepted = false
      try {
        execFileSync(
          'jq',
          ['-e', '--arg', 'route', 'false', '--arg', 'maui', 'false', expression],
          { input: JSON.stringify(needs), stdio: ['pipe', 'pipe', 'pipe'] },
        )
        accepted = true
      } catch (error) {
        assert.equal(error.status, 1)
      }
      assert.equal(accepted, succeeds, `build-ios-app result: ${result}`)
    }
  })
})
