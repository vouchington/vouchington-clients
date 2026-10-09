import assert from 'node:assert/strict'
import { chmod, mkdir, mkdtemp, readFile, readlink, rm, writeFile } from 'node:fs/promises'
import { spawnSync } from 'node:child_process'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import test from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const prePushPath = join(repositoryRoot, 'swift-clients/apps/android/tooling/pre-push.sh')
const testRunnerLinkPath = join(
  repositoryRoot,
  'swift-clients/apps/android/tooling/link-swift64-android-test-runner.sh',
)
const gradleSettingsPath = join(
  repositoryRoot,
  'swift-clients/apps/android/Android/settings.gradle.kts',
)

test('builds only the Android app Gradle task closure', async () => {
  const prePush = await readFile(prePushPath, 'utf8')
  const qualifiedAssemble = prePush.indexOf('./gradlew :app:assembleDebug')
  const unqualifiedAssemble = prePush.indexOf('./gradlew assembleDebug')
  const nativeTestBuild = prePush.indexOf('skip android build \\')
  const runnerLink = prePush.indexOf('link-swift64-android-test-runner.sh')
  const skipAndroidTest = prePush.indexOf('skip android test \\')

  assert.ok(qualifiedAssemble >= 0, 'pre-push must qualify the Android app task')
  assert.equal(unqualifiedAssemble, -1, 'pre-push must not assemble every Gradle subproject')
  assert.match(prePush, /Android Gradle :app:assembleDebug completed in/)
  assert.match(prePush, /GITHUB_STEP_SUMMARY/)
  assert.match(prePush, /Warning: unable to write GitHub step summary/)
  assert.ok(nativeTestBuild > qualifiedAssemble)
  assert.ok(runnerLink > nativeTestBuild)
  assert.ok(
    skipAndroidTest > runnerLink,
    'Skip must verify the Swift 6.4 runner before packaging Android tests',
  )
})

test('uses the materialized Swift SDK NDK for the Gradle and Skip test builds', async () => {
  const prePush = await readFile(prePushPath, 'utf8')
  const ndkVerification = prePush.indexOf('if [[ ! -f "$SKIP_NDK_SENTINEL" ]]')
  const ndkSelection = prePush.indexOf(
    'ANDROID_NDK_ROOT="${SKIP_NDK_SENTINEL%/.extraction-complete}"',
  )
  const gradleBuild = prePush.indexOf('./gradlew :app:assembleDebug')
  const skipTest = prePush.indexOf('skip android test \\')

  assert.ok(ndkVerification >= 0)
  assert.ok(ndkSelection > ndkVerification)
  assert.ok(gradleBuild > ndkSelection)
  assert.ok(skipTest > gradleBuild)
  assert.match(
    prePush,
    /ANDROID_NDK_HOME="\$ANDROID_NDK_ROOT"\n  export ANDROID_NDK_ROOT ANDROID_NDK_HOME/u,
  )
})

test('links only a verified Swift 6.4 Android test runner for Skip', async () => {
  const packageDir = await mkdtemp(join(tmpdir(), 'voucha-android-runner-'))
  const productsDir = join(packageDir, '.build/out/Products/Debug-android-aarch64')
  const runner = join(productsDir, 'VouchaAndroidTests-test-runner')
  const legacyBundle = join(productsDir, 'voucha-androidPackageTests.xctest')

  try {
    await mkdir(productsDir, { recursive: true })
    const missing = spawnSync('bash', [testRunnerLinkPath, packageDir], { encoding: 'utf8' })
    assert.notEqual(missing.status, 0)
    assert.match(missing.stderr, /test runner is missing or not executable/u)

    await writeFile(runner, '#!/bin/sh\nexit 0\n')
    await chmod(runner, 0o755)
    const linked = spawnSync('bash', [testRunnerLinkPath, packageDir], { encoding: 'utf8' })
    assert.equal(linked.status, 0, linked.stderr)
    assert.equal(await readlink(legacyBundle), 'VouchaAndroidTests-test-runner')

    const again = spawnSync('bash', [testRunnerLinkPath, packageDir], { encoding: 'utf8' })
    assert.equal(again.status, 0, again.stderr)
    assert.equal(await readlink(legacyBundle), 'VouchaAndroidTests-test-runner')

    await rm(legacyBundle)
    await writeFile(legacyBundle, '#!/bin/sh\nexit 0\n')
    await chmod(legacyBundle, 0o755)
    const stale = spawnSync('bash', [testRunnerLinkPath, packageDir], { encoding: 'utf8' })
    assert.notEqual(stale.status, 0)
    assert.match(stale.stderr, /does not point to the verified runner/u)
  } finally {
    await rm(packageDir, { recursive: true, force: true })
  }
})

test('keeps generated Skip modules out of the root Gradle project', async () => {
  const gradleSettings = await readFile(gradleSettingsPath, 'utf8')

  assert.doesNotMatch(gradleSettings, /id\("skip-plugin"\) apply true/)
  assert.match(gradleSettings, /System\.getenv\("BUILT_PRODUCTS_DIR"\)/)
  assert.match(gradleSettings, /BuildToolPluginIntermediates/)
  assert.match(gradleSettings, /generatedProjectDeclaration/)
  assert.match(gradleSettings, /StandardCopyOption\.ATOMIC_MOVE/)
  assert.match(gradleSettings, /includeBuild\(skipstoneProject\)/)
  assert.match(gradleSettings, /rootProjectPaths == listOf\(":app"\)/)
})
