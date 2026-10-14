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

describe('iOS Simulator launch smoke fan-in', () => {
  it('runs the unsigned app on Simulator and requires success', () => {
    const workflow = readFileSync(
      new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
      'utf8',
    )
    const validation = readFileSync(
      new URL('../.github/workflows/validate.yml', import.meta.url),
      'utf8',
    )
    assert.match(
      validation,
      /name: Require jq for workflow predicate tests\n\s+run: command -v jq/u,
    )
    const testsJob = jobBlock(workflow, 'tests')
    const iosJob = jobBlock(workflow, 'build-ios-app')
    assert.match(iosJob, /xcrun simctl list devices available -j/u)
    assert.match(iosJob, /if \[\[ -z "\$simulator_id" \]\]; then/u)
    assert.match(
      iosJob,
      /-only-testing:VouchaLaunchSmokeTests\/LaunchSmokeTests\/testLaunchRendersRootShell/u,
    )
    assert.match(
      iosJob,
      /xcresulttool get test-results summary[\s\S]*?\.totalTestCount == 1 and \.passedTests == 1/u,
    )
    assert.match(iosJob, /CODE_SIGNING_ALLOWED=NO/u)
    assert.match(
      iosJob,
      /if: \$\{\{ failure\(\) \}\}[\s\S]*?xcrun simctl spawn "\$simulator_id" log show/u,
    )
    assert.match(iosJob, /actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?ios-launch-smoke\.xcresult/u)
    const selector = iosJob.match(
      /simulator_id="\$\(xcrun simctl list devices available -j \| jq -r '([^']+)'\)"/u,
    )?.[1]
    assert.ok(selector)
    const devices = {
      devices: {
        'com.apple.CoreSimulator.SimRuntime.iOS-26-4': [
          { name: 'iPad Pro', udid: 'ipad', isAvailable: true },
          { name: 'iPhone 17 Pro', udid: 'iphone', isAvailable: false },
          { name: 'iPhone 17', udid: 'available-iphone', isAvailable: true },
        ],
        'com.apple.CoreSimulator.SimRuntime.watchOS-26-4': [
          { name: 'iPhone Watch', udid: 'watch', isAvailable: true },
        ],
      },
    }
    assert.equal(
      execFileSync('jq', ['-r', selector], {
        input: JSON.stringify(devices),
        encoding: 'utf8',
      }).trim(),
      'available-iphone',
    )
    assert.equal(
      execFileSync('jq', ['-r', selector], {
        input: JSON.stringify({ devices: {} }),
        encoding: 'utf8',
      }).trim(),
      '',
    )
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
          {
            input: JSON.stringify(needs),
            stdio: ['pipe', 'pipe', 'pipe'],
          },
        )
        accepted = true
      } catch (error) {
        assert.equal(error.status, 1)
      }
      assert.equal(accepted, succeeds, `build-ios-app result: ${result}`)
    }
  })
})
