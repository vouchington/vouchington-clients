import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('Android caches restore on all runs and save only completed trusted main phases', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const archives = await readFile(
    new URL('../.github/actions/prepare-android-archives/action.yml', import.meta.url),
    'utf8',
  )
  const app = workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0]
  const libs = workflow
    .split('  test-swift-android-libs:\n')[1]
    .split('  test-swift-android-host:\n')[0]
  const host = workflow
    .split('  test-swift-android-host:\n')[1]
    .split('  test-swift-android-cookie:\n')[0]
  const coreJob = workflow.split('  build-android-core:\n')[1].split('  build-macos-app:\n')[0]

  assert.match(
    app,
    /uses: \.\/\.github\/actions\/prepare-native-contract[\s\S]*?skip-candidate-install: true/u,
  )
  assert.doesNotMatch(app, /(?:pnpm|npm) (?:run|exec|install) /u)

  for (const job of [app, libs, host]) {
    assert.match(job, /uses: \.\/\.github\/actions\/prepare-android-archives/u)
  }
  assert.match(libs, /save-cache: true/u)
  for (const job of [app, host]) assert.match(job, /save-cache: false/u)
  assert.match(archives, /uses: actions\/cache\/restore@[0-9a-f]{40}/u)
  assert.match(archives, /uses: actions\/cache\/save@[0-9a-f]{40}/u)
  assert.match(
    archives,
    /hashFiles\('candidate-clients\/swift-clients\/apps\/android\/tooling\/materialize-skip-sdk\.sh'\)/u,
  )
  assert.doesNotMatch(archives, /hashFiles\([^\n]*native-contract-tests\.yml/u)
  assert.match(
    archives,
    /inputs\.host-only != 'true' && inputs\.save-cache == 'true' && github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.archives\.outputs\.cache-hit != 'true'/u,
  )
  assert.match(app, /id: android-gradle-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u)
  assert.match(
    app,
    /github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-gradle-cache\.outputs\.cache-hit != 'true'/u,
  )
  assert.match(app, /key: \$\{\{ steps\.android-gradle-cache\.outputs\.cache-primary-key \}\}/u)
  assert.ok(
    app.indexOf('Upload candidate Android APKs for runtime proof') <
      app.indexOf('Save Gradle user home from main'),
  )
  for (const job of [app]) {
    assert.match(job, /name: Select Gradle task-output cache path\n\s+id: android-task-cache-path/u)
    assert.match(
      job,
      /bash candidate-clients\/swift-clients\/apps\/android\/tooling\/gradle-build-cache-dir\.sh/u,
    )
    assert.match(job, /select-gradle-task-cache\.mjs/u)
    assert.match(
      job,
      /name: Restore Gradle task-output cache\n\s+id: android-task-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u,
    )
    assert.match(
      job,
      /TASK_CACHE_CONFIG_HASH: \$\{\{ hashFiles\('candidate-clients\/\.mise\.toml'/u,
    )
    assert.match(job, /key: \$\{\{ steps\.android-task-cache-path\.outputs\.cache-key \}\}/u)
    assert.match(
      job,
      /restore-keys: \|\n\s+\$\{\{ steps\.android-task-cache-path\.outputs\.previous-key \}\}\n\s+\$\{\{ steps\.android-task-cache-path\.outputs\.compatible-prefix \}\}/u,
    )
    assert.doesNotMatch(job, /Restore prior main Gradle task-output cache/u)
  }
  const appSteps = [
    'Prepare checksummed Swift Android SDK and archives',
    'Resolve Android Swift package with shared artifact cache',
    'Restore Gradle user home',
    'Build the selected Android validation phase',
    'Package exact candidate Android app and instrumentation APKs',
  ]
  for (const step of appSteps) assert.notEqual(app.indexOf(step), -1)
  for (let index = 1; index < appSteps.length; index += 1) {
    assert.ok(app.indexOf(appSteps[index - 1]) < app.indexOf(appSteps[index]))
  }
  assert.match(app, /! grep -E 'Downloading binary artifact' "\$resolve_log"/u)
  assert.doesNotMatch(app, /VOUCHA_ANDROID_STATE_VERIFIED|android-state-cli|android-kotlin-cache/u)
  assert.match(
    app,
    /name: Bound Gradle task-output cache save\n\s+if: \$\{\{ github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-task-cache\.outputs\.cache-hit != 'true' \}\}/u,
  )
  assert.match(
    app,
    /name: Save Gradle task-output cache from successful main APK build\n\s+if: \$\{\{ steps\.android-task-cache-save\.outputs\.save == 'true' \}\}\n\s+uses: actions\/cache\/save@[0-9a-f]{40}/u,
  )
  assert.match(app, /key: \$\{\{ steps\.android-task-cache\.outputs\.cache-primary-key \}\}/u)
  assert.ok(
    app.indexOf('Upload candidate Android APKs for runtime proof') <
      app.indexOf('Bound Gradle task-output cache save'),
  )
  for (const job of [libs, host]) {
    assert.doesNotMatch(job, /Select Gradle task-output cache path/u)
    assert.doesNotMatch(job, /Restore Gradle task-output cache/u)
  }
  for (const job of [libs, host]) assert.doesNotMatch(job, /Bound Gradle task-output cache save/u)
  assert.match(app, /TASK_CACHE_KIND: app/u)
  for (const [job, completed, kind] of [
    [app, 'Upload candidate Android APKs for runtime proof', 'APK'],
  ]) {
    assert.ok(job.indexOf(completed) < job.indexOf('Bound Gradle task-output cache save'))
    assert.match(
      job,
      /name: Bound Gradle task-output cache save\n(?:\s+timeout-minutes: 1\n)?\s+if: \$\{\{ github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-task-cache\.outputs\.cache-hit != 'true' \}\}/u,
    )
    assert.ok(job.includes(`Save Gradle task-output cache from successful main ${kind} build`))
    assert.match(job, /if: \$\{\{ steps\.android-task-cache-save\.outputs\.save == 'true' \}\}/u)
    assert.match(job, /check-gradle-build-cache\.mjs/u)
    assert.match(job, /key: \$\{\{ steps\.android-task-cache\.outputs\.cache-primary-key \}\}/u)
  }
  assert.match(coreJob, /uses: actions\/cache@[0-9a-f]{40}/u)
  assert.match(coreJob, /path: ~\/\.cache\/voucha\/swift-android\/downloads/u)
  assert.match(
    coreJob,
    /hashFiles\('candidate-clients\/swift-clients\/tooling\/build-android-core\.sh'\)/u,
  )
  assert.doesNotMatch(coreJob, /restore-keys:/u)
})

test('Android app job builds both APKs in one job without exact-run state handoffs', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const properties = await readFile(
    new URL('../swift-clients/apps/android/Android/gradle.properties', import.meta.url),
    'utf8',
  )
  const app = workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0]
  const required = workflow
    .split('  test-swift-android-cookie:\n')[1]
    .split('  build-swift-ui-tests:\n')[0]
  for (const removed of [
    'build-swift-android-dependency',
    'build-swift-android-native',
    'build-swift-android-kotlin-libraries',
    'android-native-dependency-state',
    'android-generated-state',
    'android-kotlin-cache',
  ]) {
    assert.doesNotMatch(workflow, new RegExp(removed, 'u'))
  }
  assert.match(app, /needs: verify\n/u)
  assert.match(app, /ANDROID_BUILD_PHASE: app\n/u)
  assert.match(app, /VOUCHA_SKIP_ANDROID_HOST_SWIFT_TEST: '1'/u)
  assert.match(app, /timeout-minutes: 35\n/u)
  assert.match(properties, /^org\.gradle\.parallel=true$/mu)
  assert.match(properties, /^org\.gradle\.caching=true$/mu)
  assert.match(required, /build-swift-android,/u)
  assert.match(required, /test "\$ANDROID_APP_RESULT" = success/u)
  assert.match(required, /name: Swift Android Skip/u)
  assert.match(required, /Prove checked encrypted storage survives Android process restarts/u)
  const ordered = [
    'Prepare checksummed Swift Android SDK and archives',
    'Resolve Android Swift package with shared artifact cache',
    'Restore Gradle user home',
    'Restore Gradle task-output cache',
    'Build the selected Android validation phase',
    'Package exact candidate Android app and instrumentation APKs',
    'Upload candidate Android APKs for runtime proof',
  ]
  for (const step of ordered) assert.notEqual(app.indexOf(step), -1)
  for (let index = 1; index < ordered.length; index += 1) {
    assert.ok(app.indexOf(ordered[index - 1]) < app.indexOf(ordered[index]))
  }
  assert.match(app, /verify-native-abi/u)
})
