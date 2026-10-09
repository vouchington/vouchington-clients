export const NUGET_LOCK_PATHS = Object.freeze([
  'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
  'dotnet-clients/src/Voucha.Client.App/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-arm64.lock.json',
  'dotnet-clients/src/Voucha.Client.Core/packages.net10.0-maccatalyst.maccatalyst-x64.lock.json',
  'dotnet-clients/tests/Voucha.Client.App.Tests/packages.lock.json',
  'dotnet-clients/tests/Voucha.Client.Core.Tests/packages.lock.json',
])

export const NUGET_MANIFEST_PATH = 'dotnet-clients/Directory.Packages.props'
export const DEPENDABOT_LOGIN = 'dependabot[bot]'
export const DEPENDABOT_REF_PREFIX = 'dependabot/'
export const RAW_MODIFICATION_PATTERN = /^:100644 100644 [0-9a-f]{7,64} [0-9a-f]{7,64} M$/u

const POSITIVE_INTEGER_PATTERN = /^[1-9][0-9]*$/u
const REPOSITORY_PATTERN = /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/u
const SHA_PATTERN = /^[0-9a-f]{40}$/u
const SHA256_PATTERN = /^[0-9a-f]{64}$/u

export function fail(message) {
  throw new Error(message)
}

function isRecord(value) {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export function requireRecord(value, label) {
  if (!isRecord(value)) fail(`${label} must be an object`)
  return value
}

export function requireExactKeys(value, keys, label) {
  const actual = Object.keys(requireRecord(value, label)).toSorted()
  const expected = [...keys].toSorted()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    fail(`${label} has unexpected fields`)
  }
}

export function requireSha(value, label) {
  if (typeof value !== 'string' || !SHA_PATTERN.test(value)) {
    fail(`${label} must be a lowercase 40-character Git SHA`)
  }
  return value
}

export function requireSha256(value, label) {
  if (typeof value !== 'string' || !SHA256_PATTERN.test(value)) {
    fail(`${label} must be a lowercase SHA-256 digest`)
  }
  return value
}

export function requirePositiveInteger(value, label) {
  const source = typeof value === 'number' ? String(value) : value
  if (typeof source !== 'string' || !POSITIVE_INTEGER_PATTERN.test(source)) {
    fail(`${label} must be a positive integer`)
  }
  const parsed = Number(source)
  if (!Number.isSafeInteger(parsed)) fail(`${label} must be a safe integer`)
  return parsed
}

export function requireRepository(value, label) {
  if (typeof value !== 'string' || !REPOSITORY_PATTERN.test(value)) {
    fail(`${label} must be an owner/name repository`)
  }
  return value
}

export function requireExact(value, expected, label) {
  if (value !== expected) fail(`${label} did not match the trusted context`)
}
