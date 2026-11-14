import { exactKeys, fail } from './paired-native-evidence-validation.mjs'
import { androidPhasesForProfile } from './paired-native-test-profiles.mjs'
import { validateCleanupShape } from './paired-native-cleanup-evidence.mjs'

export function validateInstrumentationResult(result, expected) {
  exactKeys(
    result,
    [
      'schema',
      'runId',
      'runAttempt',
      'nativeTestProfile',
      'clientsMergeSha',
      'apkSha256',
      'emulator',
      'phases',
      'cleanup',
    ],
    'runtime result',
  )
  if (
    result.schema !== 1 ||
    String(result.runId) !== String(expected.runId) ||
    result.runAttempt !== 1
  )
    fail('runtime result is bound to another workflow run')
  if (result.nativeTestProfile !== expected.nativeTestProfile)
    fail('runtime result does not match the selected native test profile')
  if (
    result.clientsMergeSha !== expected.clientsMergeSha ||
    result.apkSha256 !== expected.apkSha256
  )
    fail('runtime result source or APK digest is mismatched')
  exactKeys(
    result.emulator,
    ['version', 'api', 'abis', 'nativeBridge', 'buildFingerprint'],
    'emulator details',
  )
  if (
    typeof result.emulator.version !== 'string' ||
    !result.emulator.version.startsWith('37.') ||
    result.emulator.api !== 35 ||
    typeof result.emulator.buildFingerprint !== 'string' ||
    result.emulator.buildFingerprint.length < 10 ||
    !Array.isArray(result.emulator.abis) ||
    !result.emulator.abis.includes('arm64-v8a') ||
    typeof result.emulator.nativeBridge !== 'string' ||
    result.emulator.nativeBridge === '' ||
    result.emulator.nativeBridge === '0'
  )
    fail('runtime emulator does not match the fixed API 35/native bridge target')
  const expectedPhases = androidPhasesForProfile(expected.nativeTestProfile)
  if (!Array.isArray(result.phases) || result.phases.length !== expectedPhases.length)
    fail('runtime phase list is incomplete')
  for (const [index, phase] of result.phases.entries()) {
    exactKeys(phase, ['method', 'exitCode', 'tests'], `phase ${index}`)
    if (phase.method !== expectedPhases[index] || phase.exitCode !== 0 || phase.tests !== 1)
      fail(`runtime phase ${index} did not report exactly one passing test`)
  }
  validateCleanupShape(result.cleanup, { ...expected, runAttempt: 1 })
  return true
}

export function createInstrumentationResult({
  runId,
  nativeTestProfile,
  clientsMergeSha,
  apkSha256,
  emulator,
  phases,
  cleanup,
}) {
  const result = {
    schema: 1,
    runId,
    runAttempt: 1,
    nativeTestProfile,
    clientsMergeSha,
    apkSha256,
    emulator,
    phases,
    cleanup,
  }
  validateInstrumentationResult(result, { runId, nativeTestProfile, clientsMergeSha, apkSha256 })
  return result
}
