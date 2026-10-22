import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('Android caches restore on all runs and save only one completed main phase', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const archives = await readFile(
    new URL('../.github/actions/prepare-android-archives/action.yml', import.meta.url),
    'utf8',
  )
  const producer = workflow
    .split('  build-swift-android-native:\n')[1]
    .split('  build-swift-android:\n')[0]
  const app = workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0]
  const libs = workflow
    .split('  test-swift-android-libs:\n')[1]
    .split('  test-swift-android-host:\n')[0]
  const host = workflow
    .split('  test-swift-android-host:\n')[1]
    .split('  test-swift-android-cookie:\n')[0]
  const coreJob = workflow.split('  build-android-core:\n')[1].split('  build-macos-app:\n')[0]

  for (const job of [producer, app, libs, host]) {
    assert.match(job, /uses: \.\/\.github\/actions\/prepare-android-archives/u)
  }
  assert.match(libs, /save-cache: true/u)
  for (const job of [producer, app, host]) assert.match(job, /save-cache: false/u)
  assert.match(archives, /uses: actions\/cache\/restore@[0-9a-f]{40}/u)
  assert.match(archives, /uses: actions\/cache\/save@[0-9a-f]{40}/u)
  assert.match(
    archives,
    /hashFiles\('candidate-clients\/swift-clients\/apps\/android\/tooling\/materialize-skip-sdk\.sh'\)/u,
  )
  assert.doesNotMatch(archives, /hashFiles\([^\n]*native-contract-tests\.yml/u)
  assert.match(
    archives,
    /inputs\.save-cache == 'true' && github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.archives\.outputs\.cache-hit != 'true'/u,
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
  for (const job of [producer, app]) {
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
      /name: Restore prior main Gradle task-output cache\n\s+if: \$\{\{ github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-task-cache\.outputs\.cache-hit != 'true' && steps\.android-task-cache-path\.outputs\.previous-key != '' \}\}/u,
    )
    assert.match(job, /key: \$\{\{ steps\.android-task-cache-path\.outputs\.previous-key \}\}/u)
    assert.doesNotMatch(job, /restore-keys:/u)
  }
  assert.ok(
    app.indexOf('Restore verified exact-run generated Android state') <
      app.indexOf('Restore Gradle task-output cache'),
  )
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
  for (const job of [producer, libs, host])
    assert.doesNotMatch(job, /Bound Gradle task-output cache save/u)
  assert.match(coreJob, /uses: actions\/cache@[0-9a-f]{40}/u)
  assert.match(coreJob, /path: ~\/\.cache\/voucha\/swift-android\/downloads/u)
  assert.match(
    coreJob,
    /hashFiles\('candidate-clients\/swift-clients\/tooling\/build-android-core\.sh'\)/u,
  )
  assert.doesNotMatch(coreJob, /restore-keys:/u)
})
