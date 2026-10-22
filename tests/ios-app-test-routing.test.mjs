import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { jobBlock as readJobBlock } from './workflow-job-block.mjs'

const jobBlock = (workflow, job) => readJobBlock(workflow, job, `missing workflow job ${job}`)

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
    const ipadJob = jobBlock(workflow, 'build-ios-ipad')
    const buildJob = jobBlock(workflow, 'build-ios-tests')
    const x86Job = jobBlock(workflow, 'build-ios-x86')
    assert.match(buildJob, /xcrun --sdk iphonesimulator --show-sdk-version/u)
    assert.match(buildJob, /runtime_identifier="com\.apple\.CoreSimulator\.SimRuntime\.iOS-/u)
    assert.match(iosJob, /xcrun simctl list devices available -j/u)
    assert.match(iosJob, /if \[\[ -z "\$simulator_id" \]\]; then/u)
    assert.match(iosJob, /Shutdown\) xcrun simctl boot "\$simulator_id"/u)
    assert.match(
      iosJob,
      /- name: Wait for iPhone Simulator readiness[\s\S]*?simulator_id="\$\(cat "\$RUNNER_TEMP\/ios-launch-simulator-id"\)"[\s\S]*?xcrun simctl bootstatus "\$simulator_id" -b/u,
    )
    assert.match(buildJob, /build-for-testing/u)
    assert.match(
      buildJob,
      /-only-testing:VouchaLaunchSmokeTests\/LaunchSmokeTests\/testLaunchRendersRootShell/u,
    )
    assert.match(buildJob, /-testProductsPath "\$GITHUB_WORKSPACE\/Voucha\.xctestproducts"/u)
    assert.match(buildJob, /test ! -L "\$GITHUB_WORKSPACE\/Voucha\.xctestproducts"/u)
    assert.match(buildJob, /COPYFILE_DISABLE=1 tar/u)
    assert.match(
      buildJob,
      /python3 "\$GITHUB_WORKSPACE\/scripts\/verify-ios-test-tar\.py" "\$RUNNER_TEMP\/ios-test-products\.tar\.gz"/u,
    )
    assert.match(buildJob, /-destination "\$destination"/u)
    assert.match(buildJob, /arch=arm64/u)
    assert.match(x86Job, /arch=x86_64" build/u)
    assert.match(iosJob, /needs: \[verify, build-ios-tests\]/u)
    assert.match(iosJob, /actions\/download-artifact@[a-f0-9]{40}/u)
    assert.match(
      iosJob,
      /python3 "\$GITHUB_WORKSPACE\/scripts\/verify-ios-test-tar\.py" ios-test-products\.tar\.gz/u,
    )
    assert.match(iosJob, /\.revision' "\$manifest"\)" = "\$EXPECTED_REVISION"/u)
    assert.match(iosJob, /\.destination' "\$manifest"/u)
    assert.match(iosJob, /test-without-building/u)
    assert.match(iosJob, /-testProductsPath "\$bundle"/u)
    assert.match(iosJob, /test -f "\$bundle\/Info\.plist"/u)
    assert.match(iosJob, /test ! -L "\$bundle"/u)
    assert.ok(
      iosJob.indexOf('- name: Start iPhone Simulator for launch smoke') <
        iosJob.indexOf('- name: Wait for iPhone Simulator readiness'),
    )
    assert.ok(
      iosJob.indexOf('- name: Wait for iPhone Simulator readiness') <
        iosJob.indexOf('- name: Launch iOS app on Simulator'),
    )
    assert.match(iosJob, /- name: Wait for iPhone Simulator readiness\n\s+timeout-minutes: 4/u)
    assert.match(ipadJob, /- name: Wait for iPad Simulator readiness\n\s+timeout-minutes: 4/u)
    for (const job of [buildJob, x86Job, iosJob, ipadJob]) {
      assert.match(job, /\n    timeout-minutes: 9\n/u)
    }
    assert.match(ipadJob, /needs: \[verify, build-ios-tests\]/u)
    assert.match(ipadJob, /actions\/download-artifact@[a-f0-9]{40}/u)
    assert.match(ipadJob, /verify-ios-test-tar\.py/u)
    assert.match(ipadJob, /test-without-building/u)
    assert.match(ipadJob, /-testProductsPath "\$bundle"/u)
    assert.match(ipadJob, /ipad-launch-smoke\.xcresult/u)
    assert.match(ipadJob, /platform=iOS Simulator,name=\$ipad_name,OS=\$simulator_sdk,arch=arm64/u)
    assert.match(ipadJob, /test ! -L "\$bundle"/u)
    assert.match(
      ipadJob,
      /\.totalTestCount == 1 and \.passedTests == 1 and \.failedTests == 0 and \.skippedTests == 0/u,
    )
    assert.doesNotMatch(ipadJob, /-only-testing:VouchaLaunchSmokeTests\/FeedFlowTests/u)
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
    assert.match(
      iosJob,
      /test-without-building VOUCHA_FILAMENTS_CONTRACT_ROOT="\$VOUCHA_FILAMENTS_CONTRACT_ROOT"/u,
    )
    const project = readFileSync(
      new URL('../swift-clients/apps/iOS/project.yml', import.meta.url),
      'utf8',
    )
    assert.match(project, /VOUCHA_FILAMENTS_CONTRACT_ROOT: '\$\(VOUCHA_FILAMENTS_CONTRACT_ROOT\)'/u)
    assert.match(buildJob, /CODE_SIGNING_ALLOWED=NO/u)
    assert.match(
      iosJob,
      /if: \$\{\{ failure\(\) \}\}[\s\S]*?xcrun simctl spawn "\$simulator_id" log show/u,
    )
    assert.match(iosJob, /actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?ios-launch-smoke\.xcresult/u)
    const selector = iosJob.match(
      /simulator_id="\$\(xcrun simctl list devices available -j \| jq -r --arg runtime "\$runtime_identifier" --arg name "\$simulator_name" '([^']+)'\)"/u,
    )?.[1]
    const ipadSelector = ipadJob.match(
      /simulator_id="\$\(xcrun simctl list devices available -j \| jq -r --arg runtime "\$runtime_identifier" '([^']+)'\)"/u,
    )?.[1]
    assert.match(selector, /\.name == \$name/u)
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
        [
          '-r',
          '--arg',
          'runtime',
          'com.apple.CoreSimulator.SimRuntime.iOS-26-5',
          '--arg',
          'name',
          'iPhone 17',
          selector,
        ],
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
        [
          '-r',
          '--arg',
          'runtime',
          'com.apple.CoreSimulator.SimRuntime.iOS-26-5',
          '--arg',
          'name',
          'iPhone 17',
          selector,
        ],
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
    assert.match(needs, /^      - build-ios-ipad$/mu)
    assert.match(needs, /^      - build-ios-x86$/mu)

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
      const needs = {
        ...baseNeeds,
        'build-ios-app': { result },
        'build-ios-ipad': { result: 'success' },
        'build-ios-x86': { result: 'success' },
      }
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
    for (const result of ['failure', 'skipped']) {
      const needs = {
        ...baseNeeds,
        'build-ios-app': { result: 'success' },
        'build-ios-ipad': { result },
        'build-ios-x86': { result: 'success' },
      }
      assert.throws(
        () =>
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
            { input: JSON.stringify(needs), stdio: ['pipe', 'pipe', 'pipe'] },
          ),
        error => {
          assert.equal(error.status, 1)
          assert.equal(error.stderr.toString(), '')
          assert.equal(error.stdout.toString().trim(), 'false')
          return true
        },
      )
    }
    for (const result of ['failure', 'skipped']) {
      const needs = {
        ...baseNeeds,
        'build-ios-app': { result: 'success' },
        'build-ios-ipad': { result: 'success' },
        'build-ios-x86': { result },
      }
      assert.throws(
        () =>
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
            { input: JSON.stringify(needs), stdio: ['pipe', 'pipe', 'pipe'] },
          ),
        error => {
          assert.equal(error.status, 1)
          assert.equal(error.stderr.toString(), '')
          assert.equal(error.stdout.toString().trim(), 'false')
          return true
        },
      )
    }
  })
})
