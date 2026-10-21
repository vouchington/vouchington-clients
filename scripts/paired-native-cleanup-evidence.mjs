const fail = message => {
  throw new Error(`Paired native test evidence rejected: ${message}`)
}

const unknownReasons = new Set(['missing', 'ambiguous', 'truncated', 'malformed', 'bounded'])
const commandPattern =
  /^(?:emulator -avd paired-native-[0-9]{1,20}|qemu-system-x86_64(?:-headless)?(?: -avd paired-native-[0-9]{1,20})?|crashpad_handler)$/u

function exactKeys(value, keys, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value))
    fail(`${label} must be an object`)
  const actual = Object.keys(value).sort()
  const expected = [...keys].sort()
  if (JSON.stringify(actual) !== JSON.stringify(expected))
    fail(`${label} fields differ from the fixed schema`)
}

function requirePid(value, label) {
  if (!Number.isSafeInteger(value) || value <= 0) fail(`${label} pid is malformed`)
}

function validateLineageSnapshot(snapshot, avdName, label) {
  if (!snapshot || typeof snapshot !== 'object' || Array.isArray(snapshot))
    fail(`${label} must be an object`)
  if (snapshot.state === 'unknown') {
    exactKeys(snapshot, ['state', 'launcherPid', 'reason'], label)
    requirePid(snapshot.launcherPid, label)
    if (!unknownReasons.has(snapshot.reason)) fail(`${label} is malformed`)
    return
  }
  if (snapshot.state !== 'observed') fail(`${label} is malformed`)
  exactKeys(snapshot, ['state', 'launcherPid', 'processes'], label)
  requirePid(snapshot.launcherPid, label)
  if (!Array.isArray(snapshot.processes) || snapshot.processes.length > 16)
    fail(`${label} exceeds the owned process bound`)
  const seen = new Set()
  for (const [index, entry] of snapshot.processes.entries()) {
    exactKeys(entry, ['pid', 'ppid', 'pgid', 'command'], `${label} process ${index}`)
    requirePid(entry.pid, label)
    if (
      !Number.isSafeInteger(entry.ppid) ||
      entry.ppid < 0 ||
      !Number.isSafeInteger(entry.pgid) ||
      entry.pgid < 0
    )
      fail(`${label} process ${index} is malformed`)
    if (seen.has(entry.pid)) fail(`${label} process identity is ambiguous`)
    seen.add(entry.pid)
    if (typeof entry.command !== 'string' || !commandPattern.test(entry.command))
      fail(`${label} command is malformed`)
    if (entry.command.includes(' -avd ') && !entry.command.endsWith(`-avd ${avdName}`))
      fail(`${label} is bound to another AVD`)
    if (entry.pid === snapshot.launcherPid && entry.command !== `emulator -avd ${avdName}`)
      fail(`${label} launcher identity is ambiguous`)
  }
}

export function validateCleanupShape(cleanup, expected) {
  exactKeys(
    cleanup,
    [
      'schema',
      'runId',
      'runAttempt',
      'nativeTestProfile',
      'clientsMergeSha',
      'avdName',
      'launch',
      'shutdown',
    ],
    'cleanup evidence',
  )
  if (
    cleanup.schema !== 1 ||
    String(cleanup.runId) !== String(expected.runId) ||
    cleanup.runAttempt !== expected.runAttempt
  )
    fail('cleanup evidence is bound to another workflow run')
  if (cleanup.nativeTestProfile !== expected.nativeTestProfile)
    fail('cleanup evidence does not match the selected native test profile')
  if (cleanup.clientsMergeSha !== expected.clientsMergeSha)
    fail('cleanup evidence candidate merge SHA is mismatched')
  if (
    !/^[0-9]{1,20}$/u.test(String(expected.runId)) ||
    cleanup.avdName !== `paired-native-${expected.runId}`
  )
    fail('cleanup evidence AVD is not the run-owned identity')
  validateLineageSnapshot(cleanup.launch, cleanup.avdName, 'launch lineage')
  validateLineageSnapshot(cleanup.shutdown, cleanup.avdName, 'shutdown lineage')
}

export function validateCleanupEvidence(cleanup, expected) {
  exactKeys(
    expected,
    [
      'runId',
      'runAttempt',
      'nativeTestProfile',
      'clientsMergeSha',
      'runtimeArtifactId',
      'runtimeArtifactDigest',
      'boundRuntimeArtifactId',
      'boundRuntimeArtifactDigest',
    ],
    'cleanup binding',
  )
  if (
    !Number.isSafeInteger(expected.runtimeArtifactId) ||
    expected.runtimeArtifactId <= 0 ||
    expected.runtimeArtifactId !== expected.boundRuntimeArtifactId
  )
    fail('cleanup evidence runtime artifact ID does not match the uploaded artifact')
  if (
    typeof expected.runtimeArtifactDigest !== 'string' ||
    !/^[0-9a-f]{64}$/u.test(expected.runtimeArtifactDigest) ||
    expected.runtimeArtifactDigest !== expected.boundRuntimeArtifactDigest
  )
    fail('cleanup evidence runtime artifact digest does not match the uploaded artifact')
  validateCleanupShape(cleanup, expected)
  return true
}
