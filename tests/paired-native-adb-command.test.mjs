import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import { passingAdbCommand } from './paired-native-adb-command.mjs'

const callers = [
  'paired-native-android-session.test.mjs',
  'paired-native-emulator-lineage.test.mjs',
  'paired-native-lineage-bounds.test.mjs',
]

test('reports the API 35 properties a passing emulator session reads', () => {
  assert.equal(passingAdbCommand('adb', ['shell', 'getprop', 'sys.boot_completed']), '1')
  assert.equal(passingAdbCommand('adb', ['shell', 'getprop', 'ro.build.version.sdk']), '35')
  assert.equal(
    passingAdbCommand('adb', ['shell', 'getprop', 'ro.build.fingerprint']),
    'google/sdk_gphone64_x86_64/test-build',
  )
  assert.equal(
    passingAdbCommand('adb', ['shell', 'getprop', 'ro.product.cpu.abilist']),
    'x86_64,arm64-v8a',
  )
  assert.equal(
    passingAdbCommand('adb', ['shell', 'getprop', 'ro.dalvik.vm.native.bridge']),
    'libndk_translation.so',
  )
  assert.equal(passingAdbCommand('adb', ['shell', 'getprop', 'ro.build.unknown']), '')
})

test('returns a passing instrumentation result for the requested class and method', () => {
  assert.equal(
    passingAdbCommand('adb', ['shell', 'pm', 'list', 'instrumentation']),
    'instrumentation:ai.voucha.android.test/androidx.test.runner.AndroidJUnitRunner (target=ai.voucha.android)',
  )
  assert.equal(
    passingAdbCommand('adb', [
      'shell',
      'am',
      'instrument',
      '-w',
      '-r',
      '-e',
      'class',
      'voucha.android.CookieSnapshotBridgeRestartTest#seedCheckedEntries',
      'ai.voucha.android.test/androidx.test.runner.AndroidJUnitRunner',
    ]),
    [
      'INSTRUMENTATION_STATUS: class=voucha.android.CookieSnapshotBridgeRestartTest',
      'INSTRUMENTATION_STATUS: test=seedCheckedEntries',
      'INSTRUMENTATION_STATUS_CODE: 0',
      'OK (1 test)',
      'INSTRUMENTATION_CODE: -1',
    ].join('\n'),
  )
  assert.equal(passingAdbCommand('adb', ['install', '-r', 'app-debug.apk']), '')
  assert.equal(passingAdbCommand('adb', ['shell', 'am', 'force-stop', 'ai.voucha.android']), '')
})

test('session tests share the passing adb command instead of copying it', async () => {
  for (const caller of callers) {
    const source = await readFile(new URL(`./${caller}`, import.meta.url), 'utf8')
    assert.match(source, /passingAdbCommand/u)
    assert.doesNotMatch(source, /sys\.boot_completed/u)
    assert.doesNotMatch(source, /INSTRUMENTATION_STATUS: class/u)
  }
})
