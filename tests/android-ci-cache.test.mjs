import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdir, mkdtemp, readFile, rm, symlink } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

test('temporary cold diagnostic skips cache reads and rejects warm or symlinked Gradle homes', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const jobs = [
    workflow.split('  build-swift-android-native:\n')[1].split('  build-swift-android-jvm:\n')[0],
    workflow.split('  build-swift-android-jvm:\n')[1].split('  build-swift-android:\n')[0],
    workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0],
  ]
  const root = await mkdtemp(join(tmpdir(), 'voucha-android-cold-home-'))
  const home = join(root, 'home')
  const gradleHome = join(home, '.cache/voucha/gradle')
  await mkdir(home)
  try {
    for (const job of jobs) {
      assert.match(job, /VOUCHA_ANDROID_COLD_DIAGNOSTIC: '1'/u)
      for (const step of ['Restore Gradle user home', 'Restore Gradle task-output cache']) {
        assert.match(
          job,
          new RegExp(
            `name: ${step}\\n\\s+if: \\$\\{\\{ env\\.VOUCHA_ANDROID_COLD_DIAGNOSTIC != '1' \\}\\}`,
            'u',
          ),
        )
      }
      assert.match(
        job,
        /name: Restore prior main Gradle task-output cache\n\s+if: \$\{\{ env\.VOUCHA_ANDROID_COLD_DIAGNOSTIC != '1' &&/u,
      )
      const proof = job
        .split('      - name: Prove cold Gradle user home before original build\n')[1]
        .split('      - name: Build the selected Android validation phase\n')[0]
      const script = proof
        .match(/        run: \|\n((?: {10}[^\n]*\n)+)/u)?.[1]
        ?.replace(/^ {10}/gmu, '')
      assert.ok(script)
      const env = { ...process.env, HOME: home }
      delete env.GRADLE_USER_HOME
      const run = () => spawnSync('bash', ['-e', '-c', script], { env, encoding: 'utf8' })
      assert.equal(run().status, 0)
      await mkdir(gradleHome, { recursive: true })
      assert.notEqual(run().status, 0)
      await rm(gradleHome, { recursive: true })
      await symlink(home, gradleHome)
      assert.notEqual(run().status, 0)
      await rm(gradleHome)
    }
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

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
    .split('  build-swift-android-jvm:\n')[0]
  const dependency = workflow
    .split('  build-swift-android-dependency:\n')[1]
    .split('  build-swift-android-native:\n')[0]
  const jvm = workflow.split('  build-swift-android-jvm:\n')[1].split('  build-swift-android:\n')[0]
  const app = workflow.split('  build-swift-android:\n')[1].split('  test-swift-android-libs:\n')[0]
  const libs = workflow
    .split('  test-swift-android-libs:\n')[1]
    .split('  test-swift-android-host:\n')[0]
  const host = workflow
    .split('  test-swift-android-host:\n')[1]
    .split('  test-swift-android-cookie:\n')[0]
  const coreJob = workflow.split('  build-android-core:\n')[1].split('  build-macos-app:\n')[0]

  for (const job of [dependency, producer, jvm, app]) {
    assert.match(
      job,
      /uses: \.\/\.github\/actions\/prepare-native-contract[\s\S]*?skip-candidate-install: true/u,
    )
    assert.doesNotMatch(job, /(?:pnpm|npm) (?:run|exec|install) /u)
  }

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
  assert.match(
    app,
    /if: \$\{\{ env\.VOUCHA_ANDROID_COLD_DIAGNOSTIC != '1' \}\}\n\s+id: android-gradle-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u,
  )
  assert.match(
    app,
    /github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-gradle-cache\.outputs\.cache-hit != 'true'/u,
  )
  assert.match(app, /key: \$\{\{ steps\.android-gradle-cache\.outputs\.cache-primary-key \}\}/u)
  assert.ok(
    app.indexOf('Upload candidate Android APKs for runtime proof') <
      app.indexOf('Save Gradle user home from main'),
  )
  for (const job of [producer, jvm, app]) {
    assert.match(job, /name: Select Gradle task-output cache path\n\s+id: android-task-cache-path/u)
    assert.match(
      job,
      /bash candidate-clients\/swift-clients\/apps\/android\/tooling\/gradle-build-cache-dir\.sh/u,
    )
    assert.match(job, /select-gradle-task-cache\.mjs/u)
    assert.match(
      job,
      /name: Restore Gradle task-output cache\n\s+if: \$\{\{ env\.VOUCHA_ANDROID_COLD_DIAGNOSTIC != '1' \}\}\n\s+id: android-task-cache\n\s+uses: actions\/cache\/restore@[0-9a-f]{40}/u,
    )
    assert.match(
      job,
      /TASK_CACHE_CONFIG_HASH: \$\{\{ hashFiles\('candidate-clients\/\.mise\.toml'/u,
    )
    assert.match(job, /key: \$\{\{ steps\.android-task-cache-path\.outputs\.cache-key \}\}/u)
    assert.match(
      job,
      /name: Restore prior main Gradle task-output cache\n\s+if: \$\{\{ env\.VOUCHA_ANDROID_COLD_DIAGNOSTIC != '1' && github\.event_name == 'push' && github\.ref == 'refs\/heads\/main' && steps\.android-task-cache\.outputs\.cache-hit != 'true' && steps\.android-task-cache-path\.outputs\.previous-key != '' \}\}/u,
    )
    assert.match(job, /key: \$\{\{ steps\.android-task-cache-path\.outputs\.previous-key \}\}/u)
    assert.doesNotMatch(job, /restore-keys:/u)
  }
  assert.ok(
    app.indexOf('Restore verified exact-run JVM-prepared Android state') <
      app.indexOf('Restore Gradle task-output cache'),
  )
  const appSteps = [
    'Prepare checksummed Swift Android SDK and archives',
    'Download exact-run JVM-prepared Android state',
    'Restore verified exact-run JVM-prepared Android state',
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
  assert.match(jvm, /needs: \[verify, build-swift-android-native\]/u)
  assert.match(app, /needs: \[verify, build-swift-android-jvm\]/u)
  assert.match(
    jvm,
    /android-generated-state-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
  )
  assert.match(
    jvm,
    /android-jvm-state-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
  )
  assert.match(
    app,
    /android-jvm-state-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
  )
  assert.match(jvm, /ANDROID_BUILD_PHASE: app-jvm-producer/u)
  assert.doesNotMatch(jvm, /prepare-android-archives|actions\/cache\/save@/u)
  assert.ok(
    jvm.indexOf('Restore verified native producer state') <
      jvm.indexOf('Build the selected Android validation phase'),
  )
  assert.match(app, /echo 'VOUCHA_ANDROID_STATE_VERIFIED=1' >> "\$GITHUB_ENV"/u)
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
  for (const job of [producer, jvm, libs, host])
    assert.doesNotMatch(job, /Bound Gradle task-output cache save/u)
  assert.match(coreJob, /uses: actions\/cache@[0-9a-f]{40}/u)
  assert.match(coreJob, /path: ~\/\.cache\/voucha\/swift-android\/downloads/u)
  assert.match(
    coreJob,
    /hashFiles\('candidate-clients\/swift-clients\/tooling\/build-android-core\.sh'\)/u,
  )
  assert.doesNotMatch(coreJob, /restore-keys:/u)
})

test('native dependency handoff retains the original Gradle build and exact-run state boundary', async () => {
  const workflow = await readFile(
    new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
    'utf8',
  )
  const script = await readFile(
    new URL('../swift-clients/apps/android/tooling/pre-push.sh', import.meta.url),
    'utf8',
  )
  const dependency = workflow
    .split('  build-swift-android-dependency:\n')[1]
    .split('  build-swift-android-native:\n')[0]
  const native = workflow
    .split('  build-swift-android-native:\n')[1]
    .split('  build-swift-android-jvm:\n')[0]
  const required = workflow
    .split('  test-swift-android-cookie:\n')[1]
    .split('  build-swift-ui-tests:\n')[0]
  assert.match(dependency, /ANDROID_BUILD_PHASE: app-native-dependency/u)
  assert.doesNotMatch(dependency, /Restore Gradle user home|Restore Gradle task-output cache/u)
  assert.match(
    dependency,
    /android-native-dependency-state-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
  )
  assert.match(native, /needs: \[verify, build-swift-android-dependency\]/u)
  assert.match(required, /build-swift-android-dependency,/u)
  assert.match(
    required,
    /ANDROID_DEPENDENCY_RESULT: \$\{\{ needs\.build-swift-android-dependency\.result \}\}/u,
  )
  assert.match(required, /test "\$ANDROID_DEPENDENCY_RESULT" = success/u)
  assert.match(required, /name: Swift Android Skip/u)
  assert.match(required, /Prove checked encrypted storage survives Android process restarts/u)
  const ordered = [
    'Prepare checksummed Swift Android SDK and archives',
    'Download exact-run native dependency state',
    'Restore verified native dependency state',
    'Resolve Android Swift package with shared artifact cache',
    'Restore Gradle user home',
    'Build the selected Android validation phase',
  ]
  for (const step of ordered) assert.notEqual(native.indexOf(step), -1)
  for (let index = 1; index < ordered.length; index += 1) {
    assert.ok(native.indexOf(ordered[index - 1]) < native.indexOf(ordered[index]))
  }
  assert.match(native, /ANDROID_BUILD_PHASE: app-native-producer/u)
  assert.match(script, /gradle_tasks=\(:skipstone:VouchaAndroid:buildAndroidSwiftPackageDebug\)/u)
  assert.match(
    script,
    /--package-path "\$generated_package"[\s\S]*--target SkipSwiftUI[\s\S]*--scratch-path "\$generated_scratch"/u,
  )
  assert.match(
    script,
    /skip plugin --prebuild --package-path "\$ANDROID_PACKAGE_DIR" --plugin-ref "\$plugin_ref"/u,
  )
  assert.doesNotMatch(script, /gradle_tasks=\(help\)/u)
})
