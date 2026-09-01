import {
  fail,
  NUGET_LOCK_PATHS,
  NUGET_MANIFEST_PATH,
  RAW_MODIFICATION_PATTERN,
} from './validate-dependabot-dotnet-repair-shared.mjs'

export function validateDotnetRepairRawDiff(rawDiff) {
  if (typeof rawDiff !== 'string' && !Buffer.isBuffer(rawDiff)) {
    fail('raw diff must be a string or buffer')
  }
  const source = Buffer.isBuffer(rawDiff) ? rawDiff.toString('utf8') : rawDiff
  if (!source.endsWith('\0')) fail('raw diff must be NUL-delimited')
  const fields = source.slice(0, -1).split('\0')
  if (fields.length === 0 || fields.length % 2 !== 0) {
    fail('raw diff must contain complete modification records')
  }
  const allowedPaths = new Set([NUGET_MANIFEST_PATH, ...NUGET_LOCK_PATHS])
  const changedPaths = []
  for (let index = 0; index < fields.length; index += 2) {
    if (!RAW_MODIFICATION_PATTERN.test(fields[index])) {
      fail('Dependabot raw diff must contain only regular-file modifications')
    }
    const path = fields[index + 1]
    if (!allowedPaths.has(path) || changedPaths.includes(path)) {
      fail('Dependabot may change only its central manifest and existing NuGet locks')
    }
    changedPaths.push(path)
  }
  if (!changedPaths.includes(NUGET_MANIFEST_PATH)) {
    fail('Dependabot must change dotnet-clients/Directory.Packages.props')
  }
  return changedPaths
}
