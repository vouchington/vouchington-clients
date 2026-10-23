import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('direct Swift producer phases retain compiler and NDK provisioning without Java SDK setup', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  for (const [id, next, phase] of [
    ['build-swift-android-dependency', 'build-swift-android-native', 'app-native-dependency'],
    ['build-swift-android-native', 'build-swift-android', 'app-native-producer'],
  ]) {
    const job = workflow.split(`  ${id}:\n`)[1].split(`  ${next}:\n`)[0]
    assert.match(job, /uses: \.\/\.github\/actions\/setup-mise-toolchain/u)
    assert.match(job, /tool: swift/u)
    assert.match(job, /uses: \.\/\.github\/actions\/prepare-android-archives/u)
    assert.match(job, /swift package .*resolve .*--force-resolved-versions/u)
    assert.match(job, /! grep -E 'Downloading binary artifact'/u)
    assert.ok(job.includes(`ANDROID_BUILD_PHASE: ${phase}\n`))
    assert.doesNotMatch(
      job,
      /setup-android|Resolve Java|Select job-scoped Android SDK|select-xcode/u,
    )
    assert.match(job, /CANDIDATE_REVISION: \$\{\{ needs\.verify\.outputs\.revision-sha \}\}/u)
  }
  const consumer = workflow
    .split('  build-swift-android:\n')[1]
    .split('  test-swift-android-libs:\n')[0]
  assert.match(consumer, /uses: android-actions\/setup-android@[0-9a-f]{40}/u)
  assert.match(consumer, /platforms;android-36/u)
  assert.match(consumer, /build-tools;36\.0\.0/u)
  assert.match(consumer, /ANDROID_BUILD_PHASE: app-consumer/u)
  assert.match(consumer, /verify-native-abi/u)
})
