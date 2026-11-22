import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { describe, it } from 'node:test'
import { jobBlock, stepBlock } from './workflow-job-block.mjs'

const workflow = readFileSync(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)

const shardAction = readFileSync(
  new URL('../.github/actions/ios-launch-shard/action.yml', import.meta.url),
  'utf8',
)

describe('native sign-in and News CI selection (not runtime evidence)', () => {
  it('builds and independently requires News UI and iOS-only transport cases', () => {
    const project = readFileSync(
      new URL('../swift-clients/apps/iOS/project.yml', import.meta.url),
      'utf8',
    )
    assert.match(
      project,
      /VouchaFlowTransportTests:[\s\S]*?path: \.\.\/shared-app\/SourceCreationUITestingResponses\.swift/u,
    )
    const build = jobBlock(workflow, 'build-ios-tests')
    const run = jobBlock(workflow, 'build-ios-app')
    assert.match(
      build,
      /-only-testing:VouchaLaunchSmokeTests\/NewsFlowTests\/testNewsRelatedArticlesExpandAndCollapse/u,
    )
    assert.match(build, /-only-testing:VouchaFlowTransportTests\/NewsFlowTransportTests/u)
    assert.match(
      stepBlock(run, 'Launch iPhone shard news'),
      /only-testing: '-only-testing:VouchaLaunchSmokeTests\/NewsFlowTests\/testNewsRelatedArticlesExpandAndCollapse'\n\s+expected-count: 1/u,
    )
    assert.match(
      stepBlock(run, 'Launch iPhone shard news-transport'),
      /only-testing: '-only-testing:VouchaFlowTransportTests\/NewsFlowTransportTests'\n\s+expected-count: 3\n\s+spawn-gate: 'false'/u,
    )
    assert.match(shardAction, /\.failedTests == 0 and \.skippedTests == 0/u)
    assert.doesNotMatch(
      run,
      /-retry-tests-on-failure|-test-iterations|continue-on-error: true[\s\S]*test-without-building/u,
    )
  })

  it('runs independent iPad root, sign-in, and News shards from the same build', () => {
    const run = jobBlock(workflow, 'build-ios-ipad')
    assert.match(run, /name: Swift iPad Simulator launch \(\$\{\{ matrix\.group \}\}\)/u)
    assert.match(
      stepBlock(run, 'Launch iPad shard sign-in'),
      /SignInFlowTests\/testEmailOTPSignInReplacesTheSignedOutControl/u,
    )
    assert.match(
      stepBlock(run, 'Launch iPad shard news'),
      /NewsFlowTests\/testNewsRelatedArticlesExpandAndCollapse/u,
    )
    assert.match(
      stepBlock(run, 'Launch iPad shard root-shell'),
      /LaunchSmokeTests\/testLaunchRendersRootShell/u,
    )
    assert.equal(run.split('expected-count: 1').length - 1, 3)
    assert.match(shardAction, /parallel-testing-enabled NO \$ONLY_TESTING/u)
    assert.match(run, /prefix: ipad-launch/u)
    assert.doesNotMatch(run, /-retry-tests-on-failure|-test-iterations/u)
  })

  it('binds the extra transport bundle to the same immutable artifact', () => {
    const build = jobBlock(workflow, 'build-ios-tests')
    assert.match(build, /--arg flowTestBundle "\$flow_test_bundle"/u)
    assert.match(build, /flowTestBundle:\$flowTestBundle/u)
    for (const name of ['build-ios-app', 'build-ios-ipad']) {
      const consumer = jobBlock(workflow, name)
      assert.equal(
        consumer.split('for product in app runner testBundle flowTestBundle; do').length - 1,
        2,
      )
      assert.match(consumer, /VouchaFlowTransportTests\.xctest \| wc -l[^\n]+-eq 1/u)
      assert.match(consumer, /VouchaFlowTransportTests\.xctest -print -quit[^\n]+\.flowTestBundle/u)
      assert.match(consumer, /\.run' "\$manifest"/u)
      assert.match(consumer, /\.attempt' "\$manifest"/u)
      assert.match(consumer, /\.revision' "\$manifest"/u)
      assert.match(consumer, /shasum -a 256 -c -/u)
    }
  })

  it('keeps Catalyst flow evidence separate and requires its strict validator', () => {
    const job = jobBlock(workflow, 'dotnet-catalyst-gui')
    const flow = stepBlock(job, 'Exercise Catalyst email sign-in and News controls')
    assert.match(job, /\n    timeout-minutes: 15\n/u)
    assert.match(flow, /timeout-minutes: 5\n/u)
    assert.match(flow, /Contents\/Resources\/auth-news-contract/u)
    assert.match(flow, /catalyst-gui-auth-news\.mjs --stage-fixture "\$staged"/u)
    assert.match(flow, /catalyst-gui-auth-news\.mjs --session/u)
    assert.match(
      flow,
      /catalyst-auth-news-evidence\.mjs "\$CATALYST_FLOWS_RESULT" "\$CATALYST_NATIVE_REVISION"/u,
    )
    assert.doesNotMatch(flow, /continue-on-error|--session.*\|\| true/u)
    const upload = stepBlock(job, 'Upload Catalyst auth and News evidence')
    assert.match(upload, /if: \$\{\{ github\.event_name != 'push' && success\(\) \}\}/u)
    assert.match(upload, /auth-news-result\.json/u)
    assert.match(upload, /if-no-files-found: error/u)
    assert.match(upload, /actions\/upload-artifact@[a-f0-9]{40}/u)
    assert.match(job, /catalyst-notification-evidence\.mjs/u)
  })

  it('defines a zero automatic retry budget without claiming macOS flow completion', () => {
    const docs = readFileSync(
      new URL('../docs/development/native-core-ui-flows.md', import.meta.url),
      'utf8',
    )
    assert.match(docs, /automatic test-retry budget is \*\*zero\*\*/u)
    assert.match(
      docs,
      /iPad matrix preserves its root-launch\ncheck and adds separate sign-in and News shards/u,
    )
    assert.match(docs, /macOS sign-in and app-shell interactions: #80/u)
    assert.match(docs, /not producer\narticle-filter or pagination-limit parity/u)
  })

  it('runs the actual Debug-only backend source tests instead of dropping them from Release', () => {
    const step = stepBlock(workflow, 'Test Debug auth and News fixture backend')
    assert.match(step, /timeout-minutes: 3/u)
    assert.match(
      step,
      /--configuration Debug --no-restore --filter FullyQualifiedName~CatalystAuthNews/u,
    )
    assert.doesNotMatch(step, /continue-on-error/u)
  })
})
