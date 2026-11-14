import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('single Android app job provisions Swift, NDK, Java and the Android SDK for Gradle', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const job = workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0]
  assert.match(job, /uses: \.\/\.github\/actions\/setup-mise-toolchain/u)
  assert.match(job, /tool: swift/u)
  assert.match(job, /uses: \.\/\.github\/actions\/prepare-android-archives/u)
  assert.match(job, /swift package .*resolve .*--force-resolved-versions/u)
  assert.match(job, /! grep -E 'Downloading binary artifact'/u)
  assert.match(job, /Resolve Java/u)
  assert.match(job, /uses: android-actions\/setup-android@[0-9a-f]{40}/u)
  assert.match(job, /platforms;android-36/u)
  assert.match(job, /build-tools;36\.0\.0/u)
  assert.match(job, /ANDROID_BUILD_PHASE: app\n/u)
  assert.match(job, /verify-native-abi/u)
  assert.match(job, /CANDIDATE_REVISION: \$\{\{ needs\.verify\.outputs\.revision-sha \}\}/u)
})
