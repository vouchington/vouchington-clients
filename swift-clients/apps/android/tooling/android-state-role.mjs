export const testRunnerLimits = Object.freeze({
  entries: 100_000,
  unpackedBytes: 4 * 1024 ** 3,
  compressedBytes: 2 * 1024 ** 3,
  scanMs: 60_000,
  transferMs: 90_000,
})

export function isTestRunnerRole(role) {
  if (role !== 'native' && role !== 'test-runner') throw Error('Unknown Android state role')
  return role === 'test-runner'
}

export function assertStateRole(identity, role) {
  const runner = isTestRunnerRole(role)
  if (runner ? identity.role !== 'test-runner' : identity.role !== undefined) {
    throw Error('Android state role identity mismatch')
  }
}

export function assertRoleMember(path, role) {
  if (isTestRunnerRole(role) && path !== '.build' && !path.startsWith('.build/')) {
    throw Error('Android test-runner state only admits .build')
  }
}

export function stateLimits(role, nativeLimits, requested) {
  const runner = isTestRunnerRole(role)
  const maximum = runner ? testRunnerLimits : nativeLimits
  const limits = requested ?? maximum
  if (runner && (Object.keys(limits).some(key => !(key in maximum)) || Object.keys(maximum).some(key =>
    !Number.isSafeInteger(limits[key]) || limits[key] <= 0 || limits[key] > maximum[key]))) {
    throw Error('Android test-runner state limits exceed role bounds')
  }
  return limits
}
