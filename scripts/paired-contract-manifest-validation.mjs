import { declaredFilamentsContractPaths } from './contracts.mjs'
import { fail, pairedRepositories, shaIsValid } from './paired-contract-policy.mjs'

const digestPattern = /^[0-9a-f]{64}$/u
const maintainerPermissions = new Set(['admin', 'maintain'])

function exactKeys(record, expected, label) {
  if (!record || typeof record !== 'object' || Array.isArray(record))
    fail(`${label} must be an object`)
  const actual = Object.keys(record).sort()
  if (JSON.stringify(actual) !== JSON.stringify([...expected].sort()))
    fail(`${label} has an invalid key set`)
}

function validateSnapshot(snapshot, expectedRepository, expectedHead, label) {
  exactKeys(
    snapshot,
    ['repository', 'number', 'state', 'base', 'head', 'merge', 'main'],
    `${label} snapshot`,
  )
  if (
    snapshot.repository !== expectedRepository ||
    !Number.isSafeInteger(snapshot.number) ||
    snapshot.number < 1 ||
    snapshot.state !== 'open'
  )
    fail(`${label} snapshot identity is invalid`)
  exactKeys(snapshot.base, ['repository', 'ref', 'sha'], `${label} base`)
  exactKeys(snapshot.head, ['repository', 'sha'], `${label} head`)
  exactKeys(snapshot.merge, ['sha', 'parents'], `${label} merge`)
  exactKeys(snapshot.main, ['sha'], `${label} main`)
  if (
    snapshot.base.repository !== expectedRepository ||
    snapshot.base.ref !== 'main' ||
    !shaIsValid(snapshot.base.sha) ||
    typeof snapshot.head.repository !== 'string' ||
    !shaIsValid(snapshot.head.sha) ||
    snapshot.head.sha !== expectedHead ||
    !shaIsValid(snapshot.merge.sha) ||
    !shaIsValid(snapshot.main.sha) ||
    !Array.isArray(snapshot.merge.parents) ||
    snapshot.merge.parents.length !== 2 ||
    !snapshot.merge.parents.every(shaIsValid) ||
    snapshot.merge.parents[1] !== snapshot.head.sha
  )
    fail(`${label} snapshot revisions are invalid`)
}

export function validatePairManifest(value) {
  exactKeys(value, ['schemaVersion', 'attestation', 'producer', 'clients', 'contract'], 'manifest')
  if (value.schemaVersion !== 1) fail('manifest schema version is unsupported')
  exactKeys(
    value.attestation,
    [
      'actor',
      'actorPermission',
      'reviewedBothSnapshots',
      'expectedProducerHeadSha',
      'expectedClientsHeadSha',
      'workflow',
    ],
    'attestation',
  )
  if (value.attestation.reviewedBothSnapshots !== true)
    fail('manifest does not record explicit operator review')
  if (typeof value.attestation.actor !== 'string' || value.attestation.actor.trim() === '')
    fail('manifest actor is invalid')
  if (!maintainerPermissions.has(value.attestation.actorPermission))
    fail('manifest actor permission is invalid')
  if (
    !shaIsValid(value.attestation.expectedProducerHeadSha) ||
    !shaIsValid(value.attestation.expectedClientsHeadSha)
  )
    fail('manifest expected reviewed heads are invalid')
  exactKeys(
    value.attestation.workflow,
    ['repository', 'ref', 'workflowRef', 'sha', 'runId', 'runAttempt', 'name'],
    'workflow identity',
  )
  if (
    value.attestation.workflow.repository !== pairedRepositories.clients ||
    value.attestation.workflow.ref !== 'refs/heads/main' ||
    value.attestation.workflow.workflowRef !==
      `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main` ||
    !shaIsValid(value.attestation.workflow.sha) ||
    !Number.isSafeInteger(value.attestation.workflow.runAttempt) ||
    value.attestation.workflow.runAttempt !== 1 ||
    typeof value.attestation.workflow.runId !== 'string' ||
    !/^[1-9][0-9]*$/u.test(value.attestation.workflow.runId) ||
    typeof value.attestation.workflow.name !== 'string' ||
    value.attestation.workflow.name.trim() === ''
  )
    fail('manifest workflow identity is invalid')
  validateSnapshot(
    value.producer,
    pairedRepositories.producer,
    value.attestation.expectedProducerHeadSha,
    'producer',
  )
  validateSnapshot(
    value.clients,
    pairedRepositories.clients,
    value.attestation.expectedClientsHeadSha,
    'clients',
  )
  exactKeys(
    value.contract,
    ['outputOrigin', 'repository', 'ref', 'declaredPaths', 'files'],
    'contract',
  )
  if (
    value.contract.outputOrigin !== 'untrusted-candidate-exporter' ||
    value.contract.repository !== pairedRepositories.producer ||
    value.contract.ref !== 'main'
  )
    fail('manifest contract source is not the fixed producer main contract')
  if (
    JSON.stringify(value.contract.declaredPaths) !== JSON.stringify(declaredFilamentsContractPaths)
  )
    fail('manifest contract paths differ from the trusted allowlist')
  if (!Array.isArray(value.contract.files) || value.contract.files.length === 0)
    fail('manifest file list is empty')
  let previousPath = ''
  for (const record of value.contract.files) {
    exactKeys(record, ['path', 'sha256', 'sizeBytes'], 'contract file record')
    const declaredPath =
      typeof record.path === 'string'
        ? declaredFilamentsContractPaths.find(path => record.path.startsWith(`${path}/`))
        : undefined
    const relativePath =
      declaredPath === undefined ? '' : record.path.slice(declaredPath.length + 1)
    if (
      typeof record.path !== 'string' ||
      declaredPath === undefined ||
      relativePath === '' ||
      relativePath
        .split('/')
        .some(component => component === '' || component === '.' || component === '..') ||
      typeof record.sha256 !== 'string' ||
      !digestPattern.test(record.sha256) ||
      !Number.isSafeInteger(record.sizeBytes) ||
      record.sizeBytes < 0 ||
      record.path <= previousPath
    )
      fail('manifest file record is invalid or unsorted')
    previousPath = record.path
  }
  for (const declaredPath of declaredFilamentsContractPaths) {
    if (!value.contract.files.some(record => record.path.startsWith(`${declaredPath}/`)))
      fail(`manifest has no file under declared path ${declaredPath}`)
  }
  return value
}
