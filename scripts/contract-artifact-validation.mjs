import { readFile } from 'node:fs/promises'
import { isAbsolute, resolve } from 'node:path'

const shaPattern = /^[0-9a-f]{40}$/i
const repositoryPattern = /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/

export function fail(message) {
  throw new Error(`Contract artifact failed: ${message}`)
}
export function requiredString(value, label) {
  if (typeof value !== 'string' || value.trim() === '' || value.includes('\0'))
    fail(`${label} is required`)
  return value.trim()
}
export function sha(value, label) {
  const result = requiredString(value, label)
  if (!shaPattern.test(result)) fail(`${label} must be a 40-character Git SHA`)
  return result.toLowerCase()
}
export function positiveInteger(value, label) {
  const text = requiredString(String(value), label)
  if (!/^[1-9][0-9]*$/.test(text) || !Number.isSafeInteger(Number(text)))
    fail(`${label} must be a positive safe integer`)
  return text
}
export function absolutePath(value, label) {
  const result = requiredString(value, label)
  if (!isAbsolute(result)) fail(`${label} must be an absolute path`)
  return resolve(result)
}
export function artifactPath(value) {
  if (typeof value !== 'string' || value.length === 0 || value.includes('\0'))
    fail('invalid artifact path')
  if (
    value.split('/').some(component => component === '' || component === '.' || component === '..')
  )
    fail(`invalid artifact path: ${value}`)
  return value
}
export function exactKeys(value, keys, label) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) fail(`invalid ${label}`)
  if (JSON.stringify(Object.keys(value).sort()) !== JSON.stringify([...keys].sort()))
    fail(`invalid ${label}`)
}
export async function configuredPaths(configPath) {
  let config
  try {
    config = JSON.parse(await readFile(configPath, 'utf8'))
  } catch (error) {
    fail(`cannot read ${configPath}: ${error.message}`)
  }
  exactKeys(config, ['schemaVersion', 'repository', 'ref', 'paths'], 'contract configuration')
  if (config.schemaVersion !== 1) fail('unsupported contract configuration schema')
  if (!repositoryPattern.test(requiredString(config.repository, 'contract repository')))
    fail('contract repository must be owner/name')
  requiredString(config.ref, 'contract ref')
  if (!Array.isArray(config.paths) || config.paths.length === 0)
    fail('contract paths must be a nonempty array')
  const seen = new Set()
  for (const path of config.paths) {
    artifactPath(path)
    if (
      seen.has(path) ||
      [...seen].some(parent => path.startsWith(`${parent}/`) || parent.startsWith(`${path}/`))
    )
      fail('contract paths must be unique, non-overlapping directories')
    seen.add(path)
  }
  return config
}
export function createMetadata(options, config) {
  const clientsRepository = requiredString(options.clientsRepository, 'clients repository')
  if (!repositoryPattern.test(clientsRepository)) fail('clients repository must be owner/name')
  const candidateEvent = requiredString(options.candidateEvent, 'candidate event')
  if (!['pull_request', 'push'].includes(candidateEvent))
    fail('candidate event must be pull_request or push')
  const candidateNumber =
    candidateEvent === 'pull_request'
      ? Number(positiveInteger(options.candidateNumber, 'candidate PR number'))
      : null
  if (candidateEvent === 'push' && options.candidateNumber !== undefined)
    fail('push candidates must not have a PR number')
  return {
    schema: 2,
    clientsRepository,
    filaments: { repository: config.repository, sha: sha(options.filamentsSha, 'Filaments SHA') },
    candidate: {
      event: candidateEvent,
      number: candidateNumber,
      base: sha(options.baseSha, 'base SHA'),
      head: sha(options.headSha, 'head SHA'),
      revision: sha(options.revisionSha, 'candidate revision SHA'),
    },
    producer: {
      runId: positiveInteger(options.producerRunId, 'producer run ID'),
      runAttempt: positiveInteger(options.producerRunAttempt, 'producer run attempt'),
    },
  }
}
export function validateManifest(value, config) {
  exactKeys(
    value,
    [
      'schema',
      'clientsRepository',
      'filaments',
      'candidate',
      'producer',
      'allowlistedPaths',
      'files',
    ],
    'manifest',
  )
  if (value.schema !== 2) fail('unsupported manifest schema')
  if (
    !repositoryPattern.test(requiredString(value.clientsRepository, 'manifest clients repository'))
  )
    fail('invalid manifest clients repository')
  exactKeys(value.filaments, ['repository', 'sha'], 'manifest filaments metadata')
  if (value.filaments.repository !== config.repository)
    fail('invalid manifest Filaments repository')
  sha(value.filaments.sha, 'manifest Filaments SHA')
  exactKeys(value.candidate, ['event', 'number', 'base', 'head', 'revision'], 'manifest candidate')
  if (!['pull_request', 'push'].includes(value.candidate.event))
    fail('invalid manifest candidate event')
  if (value.candidate.event === 'pull_request')
    positiveInteger(value.candidate.number, 'manifest candidate PR number')
  else if (value.candidate.number !== null)
    fail('manifest push candidate must not have a PR number')
  for (const key of ['base', 'head', 'revision'])
    sha(value.candidate[key], `manifest candidate ${key} SHA`)
  exactKeys(value.producer, ['runId', 'runAttempt'], 'manifest producer metadata')
  positiveInteger(value.producer.runId, 'manifest producer run ID')
  positiveInteger(value.producer.runAttempt, 'manifest producer run attempt')
  if (
    !Array.isArray(value.allowlistedPaths) ||
    JSON.stringify(value.allowlistedPaths) !== JSON.stringify([...config.paths].sort())
  )
    fail('invalid manifest allowlisted paths')
  if (!Array.isArray(value.files)) fail('invalid manifest files')
  let previous = ''
  const seen = new Set()
  for (const record of value.files) {
    exactKeys(record, ['path', 'size', 'sha256'], 'manifest file record')
    const path = artifactPath(record.path)
    if (path <= previous || seen.has(path)) fail('manifest files must be sorted and unique')
    if (!config.paths.some(prefix => path.startsWith(`${prefix}/`)))
      fail(`unexpected manifest artifact path: ${path}`)
    if (!Number.isSafeInteger(record.size) || record.size < 0) fail('invalid manifest file size')
    if (typeof record.sha256 !== 'string' || !/^[0-9a-f]{64}$/.test(record.sha256))
      fail('invalid manifest file SHA-256')
    previous = path
    seen.add(path)
  }
  return value
}
export function checkExpected(manifest, options) {
  const pairs = [
    ['clientsRepository', manifest.clientsRepository, options.expectedClientsRepository],
    ['Filaments SHA', manifest.filaments.sha, options.expectedFilamentsSha],
    ['candidate event', manifest.candidate.event, options.expectedCandidateEvent],
    ['candidate number', String(manifest.candidate.number), options.expectedCandidateNumber],
    ['base SHA', manifest.candidate.base, options.expectedBaseSha],
    ['head SHA', manifest.candidate.head, options.expectedHeadSha],
    ['candidate revision SHA', manifest.candidate.revision, options.expectedRevisionSha],
    ['producer run ID', manifest.producer.runId, options.expectedProducerRunId],
    ['producer run attempt', manifest.producer.runAttempt, options.expectedProducerRunAttempt],
  ]
  for (const [label, actual, expected] of pairs) {
    if (expected === undefined) continue
    const normalized = label.includes('SHA')
      ? sha(expected, `expected ${label}`)
      : requiredString(String(expected), `expected ${label}`)
    if (String(actual) !== normalized) fail(`${label} mismatch`)
  }
}
