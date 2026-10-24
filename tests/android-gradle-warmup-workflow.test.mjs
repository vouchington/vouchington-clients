import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const app = workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0]
const steps = app.split('      - name: ')
const step = name => {
  const matches = steps.filter(value => value.startsWith(`${name}\n`))
  assert.equal(matches.length, 1, name)
  return matches[0]
}

test('app bootstraps after pinned tools, overlaps materialization, and joins before the full build', () => {
  const order = [
    'Resolve Java for Android SDK setup',
    'Configure app Gradle and Swift homes',
    'Restore Gradle user home',
    'Select job-scoped Android SDK location',
    'Setup Android SDK',
    'Set up mise-managed Swift compiler',
    'Select Xcode 26.6 for Android host SwiftPM tests',
    'Start isolated Gradle daemon bootstrap',
    'Prepare checksummed Swift Android SDK and archives',
    'Restore verified exact-run generated Android state',
    'Resolve Android Swift package with shared artifact cache',
    'Restore Gradle task-output cache',
    'Join isolated Gradle daemon bootstrap',
    'Build the selected Android validation phase',
  ]
  for (let i = 1; i < order.length; i++) {
    assert.ok(app.indexOf(step(order[i - 1])) < app.indexOf(step(order[i])), order[i])
  }
  const homes = step('Configure app Gradle and Swift homes')
  assert.ok(homes.includes('GRADLE_USER_HOME=$HOME/.cache/voucha/gradle'))
  assert.ok(homes.includes('VOUCHA_SKIP_SWIFT_HOME=$RUNNER_TEMP/skip-swift-home'))
  for (const [name, command] of [
    ['Start isolated Gradle daemon bootstrap', 'start'],
    ['Join isolated Gradle daemon bootstrap', 'join'],
  ]) {
    const body = step(name)
    assert.match(body, /timeout-minutes: 2/u)
    assert.match(body, /working-directory: candidate-clients/u)
    assert.ok(
      body.includes(
        'mise exec -- node swift-clients/apps/android/tooling/gradle-daemon-warmup.mjs',
      ),
    )
    assert.ok(body.includes(`gradle-daemon-warmup.mjs ${command} \\`))
    assert.ok(body.includes('--state-dir "$RUNNER_TEMP/android-gradle-warmup"'))
    assert.ok(body.includes('--wrapper swift-clients/apps/android/Android/gradlew'))
    assert.ok(body.includes('--properties swift-clients/apps/android/Android/gradle.properties'))
    assert.match(body, /--timeout-seconds 90/u)
    assert.doesNotMatch(body, /continue-on-error|\|\| true|--no-daemon/u)
  }
  const build = step('Build the selected Android validation phase')
  assert.match(build, /ANDROID_BUILD_PHASE: app-consumer/u)
  assert.ok(build.includes('pre-push.sh --phase "$ANDROID_BUILD_PHASE"'))
  assert.ok(app.includes('verify-native-abi "$app_apk"'))
  assert.match(step('Select Xcode 26.6 for Android host SwiftPM tests'), /select-xcode\.sh 26\.6/u)
})

test('app Gradle home restore retains the native producer cache contract', () => {
  const native = workflow
    .split('  build-swift-android-native:\n')[1]
    .split('  build-swift-android:\n')[0]
  const nativeRestore = native
    .split('      - name: Restore Gradle user home\n')[1]
    .split('      - name: ')[0]
  assert.equal(step('Restore Gradle user home').split('\n').slice(1).join('\n'), nativeRestore)
  assert.match(nativeRestore, /id: android-gradle-cache/u)
  assert.match(nativeRestore, /path: ~\/\.cache\/voucha\/gradle/u)
  assert.doesNotMatch(nativeRestore, /restore-keys/u)
})
