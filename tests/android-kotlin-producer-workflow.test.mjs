import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import { jobBlock } from './workflow-job-block.mjs'

const workflow = await readFile(
  new URL('../.github/workflows/native-contract-tests.yml', import.meta.url),
  'utf8',
)
const library = jobBlock(workflow, 'build-swift-android-kotlin-libraries')
const native = jobBlock(workflow, 'build-swift-android-native')
const app = jobBlock(workflow, 'build-swift-android')
const step = (job, name) => job.split(`      - name: ${name}\n`)[1].split('      - name: ')[0]

test('Kotlin libraries run beside native production with pinned host tools and no persistent writes', () => {
  assert.match(library, /needs: \[verify, build-swift-android-dependency\]/u)
  assert.match(library, /timeout-minutes: 10/u)
  assert.match(library, /ANDROID_BUILD_PHASE: app-kotlin-libraries/u)
  assert.match(library, /tool: swift/u)
  assert.match(library, /select-xcode\.sh 26\.6/u)
  assert.match(library, /uses: android-actions\/setup-android@[0-9a-f]{40}/u)
  assert.match(library, /platforms;android-36/u)
  assert.match(library, /build-tools;36\.0\.0/u)
  assert.match(step(library, 'Prepare checksummed Skip artifact only'), /timeout-minutes: 2/u)
  assert.match(library, /host-only: true/u)
  assert.match(library, /--force-resolved-versions/u)
  assert.match(library, /! grep -E 'Downloading binary artifact'/u)
  assert.equal(step(library, 'Restore Gradle user home'), step(native, 'Restore Gradle user home'))
  assert.equal(
    step(library, 'Restore Gradle task-output cache'),
    step(native, 'Restore Gradle task-output cache'),
  )
  assert.doesNotMatch(library, /actions\/cache\/save|restore-keys:|Save Gradle/u)
  assert.match(
    library,
    /name: android-native-dependency-state-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
  )
  assert.match(
    library,
    /name: android-kotlin-cache-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
  )
})

test('both siblings bind their cache provenance to the same verified dependency archive', () => {
  for (const job of [native, library]) {
    const restore = step(job, 'Restore verified native dependency state')
    assert.match(restore, /id: android-dependency-state/u)
    assert.ok(
      restore.indexOf('restore candidate-clients') <
        restore.indexOf('console.log("dependency-sha256="'),
    )
    assert.match(restore, /android-state\.json/u)
    const restoredRoot = restore.match(
      /restore candidate-clients\/swift-clients\/apps\/android "(\$RUNNER_TEMP\/[^"\n]+)"/u,
    )?.[1]
    assert.ok(restoredRoot)
    assert.ok(restore.includes(`"${restoredRoot}/android-state.json"`))
    assert.match(restore, /\.sha256/u)
  }
  assert.match(
    native,
    /dependency-sha256: \$\{\{ steps\.android-dependency-state\.outputs\.dependency-sha256 \}\}/u,
  )
  assert.match(
    step(library, 'Pack bounded exact-run Kotlin library cache'),
    /DEPENDENCY_SHA256: \$\{\{ steps\.android-dependency-state\.outputs\.dependency-sha256 \}\}/u,
  )
  assert.match(
    step(library, 'Pack bounded exact-run Kotlin library cache'),
    /--dependency-sha256 "\$DEPENDENCY_SHA256"/u,
  )
  assert.match(
    app,
    /needs: \[verify, build-swift-android-native, build-swift-android-kotlin-libraries\]/u,
  )
  const merge = step(app, 'Merge verified exact-run Kotlin library cache')
  assert.match(
    merge,
    /DEPENDENCY_SHA256: \$\{\{ needs\.build-swift-android-native\.outputs\.dependency-sha256 \}\}/u,
  )
  assert.match(merge, /--dependency-sha256 "\$DEPENDENCY_SHA256"/u)
  const ordered = [
    'Restore verified exact-run generated Android state',
    'Restore Gradle task-output cache',
    'Merge verified exact-run Kotlin library cache',
    'Build the selected Android validation phase',
    'Package exact candidate Android app and instrumentation APKs',
  ]
  for (let i = 1; i < ordered.length; i++)
    assert.ok(app.indexOf(ordered[i - 1]) < app.indexOf(ordered[i]))
  assert.match(app, /ANDROID_BUILD_PHASE: app-consumer/u)
  assert.match(app, /verify-native-abi/u)
})

test('Kotlin task graph guard observes included graphs without changing task actions or cache keys', async () => {
  const guard = await readFile(
    new URL(
      '../swift-clients/apps/android/tooling/kotlin-libraries-only.init.gradle',
      import.meta.url,
    ),
    'utf8',
  )
  assert.match(guard, /gradle\.taskGraph\.whenReady/u)
  assert.match(guard, /graph\.allTasks/u)
  assert.match(guard, /task\.class\.name/u)
  assert.match(guard, /isAnnotationPresent\(org\.gradle\.api\.tasks\.CacheableTask\)/u)
  assert.match(guard, /findProject\(':SkipUI'\)/u)
  assert.match(guard, /it\.name == 'compileDebugKotlin'/u)
  for (const forbidden of ['swift', 'jni', 'native', 'test'])
    assert.ok(guard.includes(`name.contains('${forbidden}')`))
  assert.doesNotMatch(guard, /doFirst\s*\{|doLast\s*\{|outputs\.|inputs\./u)
})
