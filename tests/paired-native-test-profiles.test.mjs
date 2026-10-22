import assert from 'node:assert/strict'
import { it } from 'node:test'
import {
  androidPhasesForProfile,
  androidTestProfiles,
} from '../scripts/paired-native-test-profiles.mjs'
import { validateInstrumentationResult } from '../scripts/paired-native-test-runtime-result.mjs'
import { cleanupFixture } from './paired-native-cleanup-fixture.mjs'

const runId = '1234'
const clientsMergeSha = 'a'.repeat(40)
const apkSha256 = 'b'.repeat(64)
const emulator = {
  version: '37.2.12.0',
  api: 35,
  abis: ['x86_64', 'arm64-v8a'],
  nativeBridge: 'libndk_translation.so',
  buildFingerprint: 'google/sdk_gphone64_x86_64/emu64xa:15/build',
}

function result(nativeTestProfile, methods) {
  return {
    schema: 1,
    runId,
    runAttempt: 1,
    nativeTestProfile,
    clientsMergeSha,
    apkSha256,
    emulator,
    phases: methods.map(method => ({ method, exitCode: 0, tests: 1 })),
    cleanup: cleanupFixture({ runId, nativeTestProfile, clientsMergeSha }),
  }
}

function expected(nativeTestProfile) {
  return { runId, nativeTestProfile, clientsMergeSha, apkSha256 }
}

it('selects only the fixed four-phase cookie or eight-phase recovery profile', () => {
  assert.equal(androidTestProfiles['cookie-snapshot'].length, 4)
  assert.equal(androidTestProfiles['pending-turn-recovery'].length, 8)
  assert.deepEqual(
    androidPhasesForProfile('cookie-snapshot'),
    androidTestProfiles['cookie-snapshot'],
  )
  assert.deepEqual(
    androidPhasesForProfile('pending-turn-recovery'),
    androidTestProfiles['pending-turn-recovery'],
  )
  for (const invalid of ['auto-detect', 'constructor', '__proto__', ''])
    assert.throws(() => androidPhasesForProfile(invalid))
})

it('validates exactly the phases selected by the trusted profile', () => {
  for (const profile of ['cookie-snapshot', 'pending-turn-recovery']) {
    const phases = androidPhasesForProfile(profile)
    assert.equal(validateInstrumentationResult(result(profile, phases), expected(profile)), true)
  }
})

it('rejects profile mismatch, automatic downgrade, missing phases, and extra phases', () => {
  const cookiePhases = androidTestProfiles['cookie-snapshot']
  const allPhases = androidTestProfiles['pending-turn-recovery']

  assert.throws(() =>
    validateInstrumentationResult(
      result('cookie-snapshot', cookiePhases),
      expected('pending-turn-recovery'),
    ),
  )
  assert.throws(() =>
    validateInstrumentationResult(
      result('pending-turn-recovery', cookiePhases),
      expected('pending-turn-recovery'),
    ),
  )
  assert.throws(() =>
    validateInstrumentationResult(
      result('cookie-snapshot', allPhases),
      expected('cookie-snapshot'),
    ),
  )
})
