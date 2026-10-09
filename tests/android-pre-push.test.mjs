import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { join, resolve } from 'node:path'
import test from 'node:test'

const repositoryRoot = resolve(import.meta.dirname, '..')
const prePushPath = join(repositoryRoot, 'swift-clients/apps/android/tooling/pre-push.sh')
const gradleSettingsPath = join(
  repositoryRoot,
  'swift-clients/apps/android/Android/settings.gradle.kts',
)

test('builds only the Android app Gradle task closure', async () => {
  const prePush = await readFile(prePushPath, 'utf8')
  const qualifiedAssemble = prePush.indexOf('./gradlew :app:assembleDebug')
  const unqualifiedAssemble = prePush.indexOf('./gradlew assembleDebug')
  const skipAndroidTest = prePush.indexOf('skip android test \\')

  assert.ok(qualifiedAssemble >= 0, 'pre-push must qualify the Android app task')
  assert.equal(unqualifiedAssemble, -1, 'pre-push must not assemble every Gradle subproject')
  assert.match(prePush, /Android Gradle :app:assembleDebug completed in/)
  assert.match(prePush, /GITHUB_STEP_SUMMARY/)
  assert.match(prePush, /Warning: unable to write GitHub step summary/)
  assert.ok(
    skipAndroidTest > qualifiedAssemble,
    'Skip Android tests must run after the app assembly',
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
