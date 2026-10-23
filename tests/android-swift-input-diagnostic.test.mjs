import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')

test('SwiftPM input snapshots are bounded, read-only, and identity-bound', () => {
  const result = spawnSync(
    'python3',
    ['-B', '-I', join(root, 'tests/test_android_swift_input_diagnostic.py')],
    {
      cwd: root,
      encoding: 'utf8',
      timeout: 30_000,
    },
  )
  assert.equal(result.status, 0, result.stderr || result.error?.message)
  assert.match(result.stderr, /Ran 8 tests/u)
})

test('Android diagnosis brackets original producer and consumer builds without changing gates', () => {
  const workflow = readFileSync(join(root, '.github/workflows/native-contract-tests.yml'), 'utf8')
  const native = workflow
    .split('  build-swift-android-native:')[1]
    .split('  build-swift-android:')[0]
  const app = workflow.split('  build-swift-android:')[1].split('  test-swift-android-libs:')[0]
  const inOrder = (text, names) => {
    let at = -1
    for (const name of names) {
      const next = text.indexOf(`- name: ${name}`, at + 1)
      assert.ok(next > at, `${name} must appear after its predecessor`)
      at = next
    }
  }
  inOrder(native, [
    'Build the selected Android validation phase',
    'Pack bounded exact-run generated Android state',
    'Upload exact-run generated Android state',
    'Snapshot producer SwiftPM inputs for diagnosis',
  ])
  inOrder(app, [
    'Restore verified exact-run generated Android state',
    'Snapshot restored consumer SwiftPM inputs for diagnosis',
    'Resolve Android Swift package with shared artifact cache',
    'Snapshot resolved consumer SwiftPM inputs for diagnosis',
    'Build the selected Android validation phase',
    'Snapshot built consumer SwiftPM inputs for diagnosis',
    'Package exact candidate Android app and instrumentation APKs',
  ])
  for (const section of [native, app]) {
    assert.match(
      section,
      /android-swift-inputs-[^\n]+-\$\{\{ needs\.verify\.outputs\.revision-sha \}\}-\$\{\{ github\.run_id \}\}-\$\{\{ github\.run_attempt \}\}/u,
    )
  }
  assert.match(app, /--phase "\$ANDROID_BUILD_PHASE"/u)
  assert.match(app, /verify-native-abi "\$app_apk"/u)
})
