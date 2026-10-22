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
  const androidJob = workflow
    .split('  build-swift-android:\n')[1]
    .split('  test-swift-android-cookie:\n')[0]
  const coreJob = workflow.split('  build-android-core:\n')[1].split('  build-macos-app:\n')[0]

  assert.match(androidJob, /uses: \.\/\.github\/actions\/prepare-android-archives/u)
  assert.match(androidJob, /save-cache: \$\{\{ matrix\.phase == 'test-libs' \}\}/u)
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
  assert.match(
    androidJob,
    /id: android-gradle-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u,
  )
  assert.match(
    androidJob,
    /matrix\.phase == 'app' && github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-gradle-cache\.outputs\.cache-hit != 'true'/u,
  )
  assert.match(
    androidJob,
    /key: \$\{\{ steps\.android-gradle-cache\.outputs\.cache-primary-key \}\}/u,
  )
  assert.ok(
    androidJob.indexOf('Upload candidate Android APKs for runtime proof') <
      androidJob.indexOf('Save Gradle user home from main'),
  )
  assert.match(
    androidJob,
    /name: Select Gradle task-output cache path\n\s+if: matrix\.phase == 'app'/u,
  )
  assert.match(
    androidJob,
    /bash candidate-clients\/swift-clients\/apps\/android\/tooling\/gradle-build-cache-dir\.sh/u,
  )
  assert.match(
    androidJob,
    /name: Restore Gradle task-output cache\n\s+if: matrix\.phase == 'app'\n\s+id: android-task-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u,
  )
  assert.match(androidJob, /path-hash=.+shasum -a 256/u)
  assert.match(
    androidJob,
    /android-gradle-tasks-v1-\$\{\{ steps\.android-task-cache-path\.outputs\.path-hash \}\}-\$\{\{ hashFiles\('candidate-clients\/\.mise\.toml'/u,
  )
  assert.match(
    androidJob,
    /name: Bound Gradle task-output cache save\n\s+if: \$\{\{ matrix\.phase == 'app' && github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-task-cache\.outputs\.cache-hit != 'true' \}\}/u,
  )
  assert.match(
    androidJob,
    /name: Save Gradle task-output cache from successful main APK build\n\s+if: \$\{\{ steps\.android-task-cache-save\.outputs\.save == 'true' \}\}\n\s+uses: actions\/cache\/save@[0-9a-f]{40}/u,
  )
  assert.match(
    androidJob,
    /key: \$\{\{ steps\.android-task-cache\.outputs\.cache-primary-key \}\}/u,
  )
  assert.ok(
    androidJob.indexOf('Upload candidate Android APKs for runtime proof') <
      androidJob.indexOf('Bound Gradle task-output cache save'),
  )
  assert.doesNotMatch(androidJob, /restore-keys:/u)
  assert.match(coreJob, /uses: actions\/cache@[0-9a-f]{40}/u)
  assert.match(coreJob, /path: ~\/\.cache\/voucha\/swift-android\/downloads/u)
  assert.match(
    coreJob,
    /hashFiles\('candidate-clients\/swift-clients\/tooling\/build-android-core\.sh'\)/u,
  )
  assert.doesNotMatch(coreJob, /restore-keys:/u)
})
