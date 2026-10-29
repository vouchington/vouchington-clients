import { mkdir, writeFile } from 'node:fs/promises'
import { join, sep } from 'node:path'
import { pairedExporterOutputPaths } from './paired-exporter-runtime-evidence.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'

const maximumOutputBytes = 200 * 1024 * 1024
export const maximumResponseBytes = 280 * 1024 * 1024

const fail = prefixedFailure('Isolated native exporter failed')

export function assertUnprivilegedHostIdentity(uid, gid) {
  if (!Number.isInteger(uid) || !Number.isInteger(gid) || uid === 0 || gid === 0)
    fail('a non-root host UID and GID are required for candidate execution')
}

function outputPath(value) {
  if (
    typeof value !== 'string' ||
    value.length === 0 ||
    value.includes('\\') ||
    value.includes('\0') ||
    value.startsWith('/') ||
    value.split('/').some(component => component === '' || component === '.' || component === '..')
  )
    fail('candidate returned a noncanonical output path')
  if (!pairedExporterOutputPaths.includes(value))
    fail(`candidate returned output outside the exact generated localization file set: ${value}`)
  return value
}

export function decodeCandidateOutput(bytes) {
  if (!Buffer.isBuffer(bytes) || bytes.length > maximumResponseBytes)
    fail('candidate response exceeds its byte limit')
  let response
  try {
    response = JSON.parse(bytes.toString('utf8'))
  } catch {
    fail('candidate response is not valid JSON')
  }
  if (
    !response ||
    Array.isArray(response) ||
    Object.keys(response).sort().join(',') !== 'files,schemaVersion' ||
    response.schemaVersion !== 1 ||
    !Array.isArray(response.files) ||
    response.files.length === 0 ||
    response.files.length > 20_000
  )
    fail('candidate response has an invalid shape')
  const seen = new Set()
  let total = 0
  const files = response.files.map(entry => {
    if (
      !entry ||
      Array.isArray(entry) ||
      Object.keys(entry).sort().join(',') !== 'contentBase64,path' ||
      typeof entry.contentBase64 !== 'string'
    )
      fail('candidate returned an invalid file entry')
    const path = outputPath(entry.path)
    if (seen.has(path)) fail(`candidate returned a duplicate path: ${path}`)
    seen.add(path)
    const contents = Buffer.from(entry.contentBase64, 'base64')
    if (contents.toString('base64') !== entry.contentBase64)
      fail(`candidate returned invalid base64 data: ${path}`)
    total += contents.length
    if (total > maximumOutputBytes) fail('candidate output exceeds its byte limit')
    return { path, contents }
  })
  if (files.length !== pairedExporterOutputPaths.length)
    fail('candidate output did not contain the exact generated localization file set')
  for (const path of pairedExporterOutputPaths)
    if (!seen.has(path)) fail(`candidate omitted declared localization output ${path}`)
  return files
}

export async function materializeOutput(outputRoot, files) {
  for (const file of files) {
    const target = join(outputRoot, ...file.path.split('/'))
    const parent = target.slice(0, target.lastIndexOf(sep))
    await mkdir(parent, { recursive: true })
    await writeFile(target, file.contents, { flag: 'wx', mode: 0o600 })
  }
}
