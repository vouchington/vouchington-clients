import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
function job(name) {
  return workflow.split(`  ${name}:\n`)[1].split(/^  [a-z][^\n]*:\n/mu)[0]
}

test('runner and library consumer retain exact candidate setup and a role-bound same-run handoff', () => {
  const producer = job('build-swift-android-test-runner')
  const consumer = job('test-swift-android-libs')
  assert.match(producer, /needs: verify\n/u)
  assert.match(producer, /timeout-minutes: 10\n/u)
  assert.match(consumer, /name: Swift Android test-libs\n/u)
  assert.match(consumer, /needs: \[verify, build-swift-android-test-runner\]/u)
  assert.match(consumer, /timeout-minutes: 20\n/u)
  for (const block of [producer, consumer]) {
    assert.match(block, /candidate-revision-sha: \$\{\{ needs.verify.outputs.revision-sha \}\}/u)
    assert.match(block, /skip-candidate-install: true/u)
    assert.match(block, /force-resolved-versions/u)
    assert.match(block, /! grep -E 'Downloading binary artifact'/u)
    assert.match(
      block,
      /android-test-runner-state-\$\{\{ needs.verify.outputs.revision-sha \}\}-\$\{\{ github.run_id \}\}-\$\{\{ github.run_attempt \}\}/u,
    )
    assert.match(block, /--role test-runner/u)
    assert.doesNotMatch(block, /actions\/cache\/save|restore-keys|Setup Android SDK|setup-java/u)
  }
  assert.match(producer, /ANDROID_BUILD_PHASE: test-runner\n/u)
  assert.match(producer, /compression-level: 0\n\s+if-no-files-found: error\n\s+retention-days: 1/u)
  assert.match(
    consumer,
    /ANDROID_BUILD_PHASE: test-libs-consumer\n\s+VOUCHA_ANDROID_STATE_VERIFIED: '1'/u,
  )
  assert.match(
    consumer,
    /repository: \$\{\{ github.repository \}\}\n\s+run-id: \$\{\{ github.run_id \}\}/u,
  )
  assert.ok(
    consumer.indexOf('Restore verified Android test-runner state') <
      consumer.indexOf('Resolve Android Swift package'),
  )
  assert.ok(
    consumer.indexOf('Resolve Android Swift package') <
      consumer.indexOf('Build the selected Android validation phase'),
  )
})

test('SDK setup stays outside candidate .build before the absent-destination restore', async () => {
  const materializer = await readFile(
    new URL('../swift-clients/apps/android/tooling/materialize-skip-sdk.sh', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(materializer, /\.build|swift package|swift build/u)
  assert.match(materializer, /SWIFT_SDKS_DIR="\$HOME\/Library\/org.swift.swiftpm\/swift-sdks"/u)
  const consumer = job('test-swift-android-libs')
  assert.ok(
    consumer.indexOf('Prepare checksummed Swift Android SDK') <
      consumer.indexOf('Restore verified Android test-runner state'),
  )
  const gate = job('test-swift-android-cookie')
  assert.match(gate, /build-swift-android-test-runner,/u)
  assert.match(
    gate,
    /ANDROID_TEST_RUNNER_RESULT: \$\{\{ needs.build-swift-android-test-runner.result \}\}/u,
  )
  assert.match(gate, /test "\$ANDROID_TEST_RUNNER_RESULT" = success/u)
  assert.match(job('tests'), /test-swift-android-cookie/u)
})
