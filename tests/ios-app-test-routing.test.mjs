import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { jobBlock as readJobBlock, stepBlock } from './workflow-job-block.mjs'

const jobBlock = (workflow, job) => readJobBlock(workflow, job, `missing workflow job ${job}`)

describe('iOS Simulator launch, presentation, feed-flow, sign-in, and News fan-in', () => {
  it('requires the unsigned shell, modal presentation, feed, sign-in, and News interaction shards', () => {
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
    const shardAction = readFileSync(
      new URL('../.github/actions/ios-launch-shard/action.yml', import.meta.url),
      'utf8',
    )
    const diagnosticsAction = readFileSync(
      new URL('../.github/actions/ios-launch-diagnostics/action.yml', import.meta.url),
      'utf8',
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
    assert.match(iosJob, /Shutdown\) date \+%s > [^;]+; xcrun simctl boot "\$simulator_id"/u)
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
    assert.equal(shardAction.split('-collect-test-diagnostics never').length - 1, 1)
    assert.doesNotMatch(iosJob + ipadJob, /xcodebuild -testProductsPath/u)
    assert.match(iosJob, /fail-fast: false/u)
    // Two iPhone jobs and two iPad jobs boot one Simulator each and run their shards in turn.
    assert.match(
      iosJob,
      /group: shell-feed-transport\n\s+shards: '\|news-transport\|root-shell\|feed\|'/u,
    )
    assert.match(iosJob, /group: sign-in-news\n\s+shards: '\|sign-in\|news\|'/u)
    assert.match(ipadJob, /group: root-shell-news\n\s+shards: '\|root-shell\|news\|'/u)
    assert.match(ipadJob, /group: sign-in\n\s+shards: '\|sign-in\|'/u)
    assert.equal(iosJob.split('expected-count: 1').length - 1, 3)
    assert.match(
      stepBlock(iosJob, 'Launch iPhone shard root-shell'),
      /only-testing: '[^'\n]*-only-testing:VouchaPresentationTests'\n\s+expected-count: 7/u,
    )
    assert.match(
      stepBlock(iosJob, 'Launch iPhone shard news-transport'),
      /expected-count: 3\n\s+spawn-gate: 'false'/u,
    )
    for (const [job, label, shards] of [
      [iosJob, 'iPhone', ['news-transport', 'root-shell', 'feed', 'sign-in', 'news']],
      [ipadJob, 'iPad', ['root-shell', 'news', 'sign-in']],
    ]) {
      for (const shard of shards) {
        // Every shard runs even after an earlier shard failed, and names itself on failure.
        const launch = stepBlock(job, `Launch ${label} shard ${shard}`)
        assert.match(launch, new RegExp(`id: shard-${shard}\\n`, 'u'))
        assert.match(
          launch,
          /if: \$\{\{ !cancelled\(\) && steps\.lldb\.outcome == 'success' && contains\(matrix\.shards, '\|[a-z-]+\|'\) \}\}/u,
        )
        assert.match(launch, /uses: \.\/\.github\/actions\/ios-launch-shard\n/u)
        const diagnostics = stepBlock(job, `Collect ${label} diagnostics for shard ${shard}`)
        assert.match(diagnostics, new RegExp(`steps\\.shard-${shard}\\.outcome == 'failure'`, 'u'))
        assert.match(diagnostics, /artifact-suffix: \$\{\{ matrix\.group \}\}/u)
      }
      assert.match(
        stepBlock(job, `Shut down ${label} Simulator`),
        /if: \$\{\{ !cancelled\(\) \}\}/u,
      )
    }
    assert.match(shardAction, /\$PREFIX-\$SHARD\.xcresult/u)
    assert.match(
      diagnosticsAction,
      /inputs\.prefix \}\}-diagnostics-.*inputs\.artifact-suffix \}\}-\$\{\{ inputs\.shard/u,
    )
    assert.match(iosJob, /skip-candidate-install: true/u)
    assert.match(ipadJob, /skip-candidate-install: true/u)
    assert.equal(iosJob.split('simctl shutdown').length - 1, 1)
    assert.equal(ipadJob.split('simctl shutdown').length - 1, 1)
    assert.doesNotMatch(buildJob, /skip-candidate-install: true/u)
    assert.match(shardAction, /test-without-building/u)
    assert.match(shardAction, /-testProductsPath "\$bundle"/u)
    assert.match(iosJob, /test -f "\$bundle\/Info\.plist"/u)
    assert.match(iosJob, /test ! -L "\$bundle"/u)
    const prepareIndex = iosJob.indexOf('uses: ./.github/actions/prepare-native-contract')
    const unpackIndex = iosJob.indexOf('- name: Verify and unpack iOS test products')
    const startupIndex = iosJob.indexOf('- name: Start iPhone Simulator for launch smoke')
    const waitIndex = iosJob.indexOf('- name: Wait for iPhone Simulator readiness')
    assert.ok(prepareIndex >= 0)
    assert.ok(unpackIndex >= 0)
    assert.ok(startupIndex >= 0)
    assert.ok(waitIndex >= 0)
    assert.ok(startupIndex < prepareIndex)
    assert.ok(prepareIndex < waitIndex)
    assert.ok(waitIndex < unpackIndex)
    // Passing unpacks took 8 to 176 s on starved runners; a 3-minute cap failed run 44121822954.
    for (const job of [iosJob, ipadJob]) {
      const unpack = stepBlock(job, 'Verify and unpack iOS test products')
      assert.match(unpack, /\n        timeout-minutes: 6\n/u)
    }
    assert.ok(
      iosJob.indexOf('- name: Wait for iPhone Simulator readiness') <
        iosJob.indexOf('- name: Launch iPhone shard news-transport'),
    )
    assert.match(stepBlock(iosJob, 'Launch iPhone shard feed'), /\n        timeout-minutes: 7\n/u)
    assert.match(stepBlock(ipadJob, 'Launch iPad shard sign-in'), /\n        timeout-minutes: 9\n/u)
    const spawnGate = readFileSync(
      new URL('../scripts/ios-launch-spawn-gate.py', import.meta.url),
      'utf8',
    )
    assert.match(spawnGate, /BUDGET_SECONDS = 150/u)
    assert.match(spawnGate, /TERMINATE_SECONDS = 60/u)
    assert.match(spawnGate, /RECOVERY_SECONDS = 120/u)
    assert.doesNotMatch(spawnGate, /LIVENESS/u)
    assert.match(spawnGate, /45060434211[\s\S]*45082704348/u)
    assert.match(spawnGate, /os\.kill\(pid, 0\)/u)
    assert.doesNotMatch(spawnGate, /launchctl/u)
    assert.match(spawnGate, /stopped-late/u)
    assert.match(
      spawnGate,
      /terminate-budget-exhausted[\s\S]*app_alive[\s\S]*reset_app[\s\S]*return 1/u,
    )
    assert.doesNotMatch(spawnGate, /erase/u)
    assert.match(spawnGate, /xcrun", "simctl", "uninstall"/u)
    assert.match(spawnGate, /xcrun", "simctl", "install"/u)
    assert.match(spawnGate, /xcrun", "simctl", "launch"/u)
    assert.match(spawnGate, /xcrun", "simctl", "terminate"/u)
    assert.match(spawnGate, /if not pid\.isdigit\(\)/u)
    assert.match(spawnGate, /Installed app did not start a process/u)
    assert.match(spawnGate, /Spawn gate exceeded 150s; continuing to xcodebuild/u)
    for (const [job, launchName] of [
      [iosJob, '- name: Launch iPhone shard news-transport'],
      [ipadJob, '- name: Launch iPad shard root-shell'],
    ]) {
      // Xcode's debugger-version snapshot stalled ~40-60s on cold runners and spent the
      // XCUIApplication launch budget; warming LLDB just before the launch step keeps it short.
      const warm = stepBlock(job, 'Warm LLDB for the Xcode launch')
      assert.match(warm, /timeout-minutes: 3\n/u)
      assert.doesNotMatch(warm, /continue-on-error/u)
      // The 120s command deadline stays under the 3-minute step cap, so a slow warm only warns
      // instead of failing the step and skipping the launch.
      assert.match(
        warm,
        /run: time perl -e 'alarm shift; exec @ARGV' 120 xcrun lldb --batch --one-line version \|\| echo "::warning::LLDB warm failed or passed 120s/u,
      )
      assert.ok(job.includes(`continuing to the launch"\n      ${launchName}\n`))
      const launch = shardAction
      const gateAt = launch.indexOf(
        'python3 "$GITHUB_WORKSPACE/scripts/ios-launch-spawn-gate.py" "$simulator_id" "$app"',
      )
      const testAt = launch.indexOf('xcodebuild -testProductsPath "$bundle"')
      assert.ok(gateAt >= 0 && testAt > gateAt)
      assert.match(launch, /case "\$app" in\n\s+"\$bundle"\/\*\) ;;/u)
    }
    assert.match(iosJob, /\n    timeout-minutes: 25\n/u)
    assert.match(buildJob, /\n    timeout-minutes: 13\n/u)
    assert.match(x86Job, /\n    timeout-minutes: 12\n/u)
    assert.match(ipadJob, /\n    timeout-minutes: 25\n/u)
    const ipadPrepare = ipadJob.indexOf('uses: ./.github/actions/prepare-native-contract')
    const ipadBoot = ipadJob.indexOf('- name: Start iPad Simulator for root-shell launch')
    const ipadDownload = ipadJob.indexOf('- name: Download iOS test products')
    const ipadWait = ipadJob.indexOf('- name: Wait for iPad Simulator readiness')
    const ipadUnpack = ipadJob.indexOf('- name: Verify and unpack iOS test products')
    const ipadLaunch = ipadJob.indexOf('- name: Launch iPad shard root-shell')
    assert.ok(ipadPrepare >= 0 && ipadBoot > ipadPrepare)
    assert.ok(ipadDownload > ipadBoot && ipadWait > ipadDownload)
    assert.ok(ipadUnpack > ipadWait && ipadLaunch > ipadUnpack)
    assert.equal(ipadJob.split('- name: Start iPad Simulator for root-shell launch').length - 1, 1)
    assert.equal(ipadJob.split('- name: Wait for iPad Simulator readiness').length - 1, 1)
    assert.match(ipadJob, /needs: \[verify, build-ios-tests\]/u)
    assert.match(ipadJob, /actions\/download-artifact@[a-f0-9]{40}/u)
    assert.match(ipadJob, /verify-ios-test-tar\.py/u)
    assert.match(ipadJob, /prefix: ipad-launch/u)
    assert.match(ipadJob, /platform=iOS Simulator,id=\$simulator_id/u)
    assert.match(ipadJob, /test ! -L "\$bundle"/u)
    assert.match(
      shardAction,
      /\.totalTestCount == \$count and \.passedTests == \$count and \.failedTests == 0 and \.skippedTests == 0/u,
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
      shardAction,
      /xcresulttool get test-results summary[\s\S]*?\.totalTestCount == \$count and \.passedTests == \$count and \.failedTests == 0 and \.skippedTests == 0/u,
    )
    assert.match(
      shardAction,
      /test-without-building VOUCHA_CONTRACT_ROOT="\$VOUCHA_CONTRACT_ROOT"/u,
    )
    const project = readFileSync(
      new URL('../swift-clients/apps/iOS/project.yml', import.meta.url),
      'utf8',
    )
    assert.match(project, /VOUCHA_CONTRACT_ROOT: '\$\(VOUCHA_CONTRACT_ROOT\)'/u)
    assert.match(buildJob, /CODE_SIGNING_ALLOWED=NO/u)
    assert.match(diagnosticsAction, /xcrun simctl spawn "\$simulator_id" log show/u)
    assert.match(diagnosticsAction, /actions\/upload-artifact@[a-f0-9]{40}[\s\S]*?\.xcresult/u)
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
      .slice(workflow.lastIndexOf('jq -e --arg event'))
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
        'test-swift-android-cookie',
        'test-swift-ui',
        'swift-patch-coverage',
        'build-android-core',
        'build-macos-app',
        'dotnet-maui',
        'dotnet-catalyst-gui',
        'dotnet-windows-image-preview',
        'dotnet-windows-language-model-surface',
        'dotnet-windows-maui',
      ].map(job => [job, { result: 'success' }]),
    )
    baseNeeds['dotnet-windows-image-preview'] = { result: 'skipped' }
    baseNeeds['dotnet-windows-language-model-surface'] = { result: 'skipped' }
    baseNeeds['dotnet-windows-maui'] = { result: 'skipped' }

    const routeArgs = ['event', 'route', 'maui', 'surface', 'docs'].flatMap(name => [
      '--arg',
      name,
      'false',
    ])
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
        execFileSync('jq', ['-e', ...routeArgs, expression], {
          input: JSON.stringify(needs),
          stdio: ['pipe', 'pipe', 'pipe'],
        })
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
          execFileSync('jq', ['-e', ...routeArgs, expression], {
            input: JSON.stringify(needs),
            stdio: ['pipe', 'pipe', 'pipe'],
          }),
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
          execFileSync('jq', ['-e', ...routeArgs, expression], {
            input: JSON.stringify(needs),
            stdio: ['pipe', 'pipe', 'pipe'],
          }),
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
