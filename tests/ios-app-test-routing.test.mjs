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

describe('iOS Simulator launch, feed-flow, and sign-in fan-in', () => {
  it('runs the unsigned app shell, canonical feed flow, and email sign-in and requires all three to pass', () => {
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
    assert.match(iosJob, /xcrun --sdk iphonesimulator --show-sdk-version/u)
    assert.match(iosJob, /runtime_identifier="com\.apple\.CoreSimulator\.SimRuntime\.iOS-/u)
    assert.match(iosJob, /xcrun simctl list devices available -j/u)
    assert.match(iosJob, /if \[\[ -z "\$simulator_id" \]\]; then/u)
    assert.match(iosJob, /Shutdown\) xcrun simctl boot "\$simulator_id"/u)
    assert.match(
      iosJob,
      /- name: Wait for iPhone Simulator readiness[\s\S]*?simulator_id="\$\(cat "\$RUNNER_TEMP\/ios-launch-simulator-id"\)"[\s\S]*?xcrun simctl bootstatus "\$simulator_id" -b/u,
    )
    assert.ok(
      iosJob.indexOf('- name: Start iPhone Simulator for launch smoke') <
        iosJob.indexOf('- name: Build iOS app for Simulator'),
    )
    assert.ok(
      iosJob.indexOf('- name: Build iOS app for Simulator') <
        iosJob.indexOf('- name: Wait for iPhone Simulator readiness'),
    )
    assert.ok(
      iosJob.indexOf('- name: Wait for iPhone Simulator readiness') <
        iosJob.indexOf('- name: Launch iOS app on Simulator'),
    )
    assert.match(iosJob, /- name: Wait for iPhone Simulator readiness\n\s+timeout-minutes: 12/u)
    assert.match(iosJob, /\n\s+timeout-minutes: 55\n/u)
    assert.ok(
      iosJob.indexOf('- name: Launch iOS app on Simulator') <
        iosJob.indexOf('- name: Shut down iPhone Simulator after launch smoke'),
    )
    assert.ok(
      iosJob.indexOf('- name: Shut down iPhone Simulator after launch smoke') <
        iosJob.indexOf('- name: Start iPad Simulator for root-shell launch'),
    )
    assert.ok(
      iosJob.indexOf('- name: Start iPad Simulator for root-shell launch') <
        iosJob.indexOf('- name: Wait for iPad Simulator readiness'),
    )
    assert.ok(
      iosJob.indexOf('- name: Wait for iPad Simulator readiness') <
        iosJob.indexOf('- name: Launch iPad root shell on Simulator'),
    )
    assert.match(iosJob, /xcrun simctl shutdown "\$simulator_id"/u)
    const ipadLaunch = iosJob.slice(iosJob.indexOf('- name: Launch iPad root shell on Simulator'))
    const ipadStep = ipadLaunch.slice(0, ipadLaunch.indexOf('\n      - name:'))
    assert.match(
      ipadStep,
      /-only-testing:VouchaLaunchSmokeTests\/LaunchSmokeTests\/testLaunchRendersRootShell/u,
    )
    assert.doesNotMatch(ipadStep, /FeedFlowTests/u)
    assert.match(ipadStep, /ipad-launch-smoke\.xcresult/u)
    assert.match(
      ipadStep,
      /xcresulttool get test-results summary[\s\S]*?\.totalTestCount == 1 and \.passedTests == 1 and \.failedTests == 0 and \.skippedTests == 0/u,
    )
    assert.match(
      iosJob,
      /-only-testing:VouchaLaunchSmokeTests\/LaunchSmokeTests\/testLaunchRendersRootShell/u,
    )
    assert.match(
      iosJob,
      /-only-testing:VouchaLaunchSmokeTests\/FeedFlowTests\/testVideoFeedLoadsCanonicalRowAndSurvivesTabNavigation/u,
    )
    assert.match(
      iosJob,
      /-only-testing:VouchaLaunchSmokeTests\/SignInFlowTests\/testEmailOTPSignInReplacesTheSignedOutControl/u,
    )
    assert.match(
      iosJob,
      /xcresulttool get test-results summary[\s\S]*?\.totalTestCount == 3 and \.passedTests == 3 and \.failedTests == 0 and \.skippedTests == 0/u,
    )
    assert.match(iosJob, /test VOUCHA_FILAMENTS_CONTRACT_ROOT="\$VOUCHA_FILAMENTS_CONTRACT_ROOT"/u)
    const project = readFileSync(
      new URL('../swift-clients/apps/iOS/project.yml', import.meta.url),
      'utf8',
    )
    assert.match(project, /VOUCHA_FILAMENTS_CONTRACT_ROOT: '\$\(VOUCHA_FILAMENTS_CONTRACT_ROOT\)'/u)
    assert.match(iosJob, /CODE_SIGNING_ALLOWED=NO/u)
    assert.match(
      iosJob,
      /if: \$\{\{ failure\(\) \}\}[\s\S]*?xcrun simctl spawn "\$simulator_id" log show/u,
    )
    assert.match(iosJob, /actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?ios-launch-smoke\.xcresult/u)
    const selectors = [
      ...iosJob.matchAll(
        /simulator_id="\$\(xcrun simctl list devices available -j \| jq -r --arg runtime "\$runtime_identifier" '([^']+)'\)"/gu,
      ),
    ].map(match => match[1])
    assert.equal(selectors.length, 2)
    const selector = selectors[0]
    const ipadSelector = selectors[1]
    assert.match(selector, /startswith\("iPhone "\)/u)
    assert.match(ipadSelector, /startswith\("iPad "\)/u)
    assert.ok(selector)
    assert.ok(ipadSelector)
    const devices = {
      devices: {
        'com.apple.CoreSimulator.SimRuntime.iOS-26-2': [
          { name: 'iPhone 17 Pro', udid: 'older-iphone', isAvailable: true },
        ],
        'com.apple.CoreSimulator.SimRuntime.iOS-26-4': [
          { name: 'iPad Pro', udid: 'ipad', isAvailable: true },
          { name: 'iPhone 17 Pro', udid: 'migrating-iphone', isAvailable: true },
        ],
        'com.apple.CoreSimulator.SimRuntime.iOS-26-5': [
          { name: 'iPhone 17 Pro', udid: 'iphone', isAvailable: false },
          { name: 'iPhone 17', udid: 'available-iphone', isAvailable: true },
        ],
        'com.apple.CoreSimulator.SimRuntime.watchOS-26-4': [
          { name: 'iPhone Watch', udid: 'watch', isAvailable: true },
        ],
      },
    }
    assert.equal(
      execFileSync(
        'jq',
        ['-r', '--arg', 'runtime', 'com.apple.CoreSimulator.SimRuntime.iOS-26-5', selector],
        {
          input: JSON.stringify(devices),
          encoding: 'utf8',
        },
      ).trim(),
      'available-iphone',
    )
    assert.equal(
      execFileSync(
        'jq',
        ['-r', '--arg', 'runtime', 'com.apple.CoreSimulator.SimRuntime.iOS-26-5', selector],
        {
          input: JSON.stringify({
            devices: {
              'com.apple.CoreSimulator.SimRuntime.iOS-26-4': [
                { name: 'iPhone 17 Pro', udid: 'older-iphone', isAvailable: true },
              ],
            },
          }),
          encoding: 'utf8',
        },
      ).trim(),
      '',
    )
    assert.equal(
      execFileSync(
        'jq',
        ['-r', '--arg', 'runtime', 'com.apple.CoreSimulator.SimRuntime.iOS-26-5', ipadSelector],
        {
          input: JSON.stringify({
            devices: {
              'com.apple.CoreSimulator.SimRuntime.iOS-26-5': [
                { name: 'iPhone 17', udid: 'iphone', isAvailable: true },
                { name: 'iPad', udid: 'bare-ipad', isAvailable: true },
                { name: 'iPad Pro 13-inch', udid: 'ipad-pro', isAvailable: true },
                { name: 'iPad Air', udid: 'unavailable-ipad', isAvailable: false },
              ],
            },
          }),
          encoding: 'utf8',
        },
      ).trim(),
      'ipad-pro',
    )
    assert.equal(
      execFileSync(
        'jq',
        ['-r', '--arg', 'runtime', 'com.apple.CoreSimulator.SimRuntime.iOS-26-5', ipadSelector],
        {
          input: JSON.stringify({
            devices: {
              'com.apple.CoreSimulator.SimRuntime.iOS-26-5': [
                { name: 'iPhone 17', udid: 'iphone', isAvailable: true },
              ],
            },
          }),
          encoding: 'utf8',
        },
      ).trim(),
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
        'dotnet-windows-language-model-surface',
        'dotnet-windows-maui',
      ].map(job => [job, { result: 'success' }]),
    )
    baseNeeds['dotnet-windows-image-preview'] = { result: 'skipped' }
    baseNeeds['dotnet-windows-language-model-surface'] = { result: 'skipped' }
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
          [
            '-e',
            '--arg',
            'route',
            'false',
            '--arg',
            'maui',
            'false',
            '--arg',
            'surface',
            'false',
            expression,
          ],
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
