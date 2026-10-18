export const pairedRepositories = Object.freeze({
  clients: 'vouchington/vouchington-clients',
  producer: 'vouchington/vouchington',
})

const shaPattern = /^[0-9a-f]{40}$/u
const numberPattern = /^[1-9][0-9]{0,8}$/u
const maintainerPermissions = new Set(['admin', 'maintain'])

function fail(message) {
  throw new Error(`Paired contract attestation failed: ${message}`)
}

function requiredString(value, label) {
  if (typeof value !== 'string' || value.trim() === '') fail(`${label} is required`)
  return value.trim()
}

function requiredSha(value, label) {
  if (typeof value !== 'string' || !shaPattern.test(value))
    fail(`${label} must be a lowercase 40-character commit SHA`)
  return value
}

function requiredNumber(value, label) {
  if (typeof value !== 'string' || !numberPattern.test(value))
    fail(`${label} must be a positive pull request number`)
  return value
}

export function validateDispatch({
  eventName,
  repository,
  ref,
  workflowRef,
  runAttempt,
  actor,
  triggeringActor,
  inputs,
  clientsPermission,
}) {
  if (eventName !== 'workflow_dispatch') fail('only workflow_dispatch is supported')
  if (repository !== pairedRepositories.clients)
    fail('workflow must run in the canonical clients repository')
  if (ref !== 'refs/heads/main') fail('workflow must be dispatched from clients main')
  if (
    workflowRef !==
    `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main`
  )
    fail('workflow definition must come from the canonical main branch')
  if (String(runAttempt) !== '1') fail('workflow reruns cannot reuse an operator attestation')
  const confirmed =
    inputs?.reviewedBothSnapshots === true || inputs?.reviewedBothSnapshots === 'true'
  if (!confirmed) fail('operator must confirm that both exact snapshots were reviewed')
  const normalizedActor = requiredString(actor, 'workflow actor')
  if (requiredString(triggeringActor, 'triggering actor') !== normalizedActor)
    fail('a rerun by a different actor cannot reuse the original attestation')
  if (!maintainerPermissions.has(clientsPermission))
    fail('workflow actor must have live maintain or admin permission in the clients repository')

  return Object.freeze({
    actor: normalizedActor,
    clientsNumber: requiredNumber(inputs?.clientsPullRequest, 'clients pull request number'),
    expectedClientsHeadSha: requiredSha(
      inputs?.expectedClientsHeadSha,
      'expected clients head SHA',
    ),
    expectedProducerHeadSha: requiredSha(
      inputs?.expectedProducerHeadSha,
      'expected producer head SHA',
    ),
    producerNumber: requiredNumber(inputs?.producerPullRequest, 'producer pull request number'),
    reviewedBothSnapshots: true,
    clientsPermission,
  })
}

function repositoryName(value, label) {
  if (!value || typeof value.full_name !== 'string') fail(`${label} repository identity is missing`)
  return value.full_name.toLowerCase()
}

function pullRequestSnapshot(repository, number, pullRequest) {
  if (pullRequest.number !== Number(number))
    fail(`${repository} API response returned a different pull request`)
  if (pullRequest.state !== 'open') fail(`${repository} pull request ${number} is not open`)
  if (repositoryName(pullRequest.base?.repo, `${repository} base`) !== repository)
    fail(`${repository} pull request targets a different repository`)
  if (pullRequest.base?.ref !== 'main') fail(`${repository} pull request must target main`)
  const baseSha = requiredSha(pullRequest.base?.sha, `${repository} base SHA`)
  const headSha = requiredSha(pullRequest.head?.sha, `${repository} head SHA`)
  const mergeSha = requiredSha(pullRequest.merge_commit_sha, `${repository} merge SHA`)
  const headRepository = repositoryName(pullRequest.head?.repo, `${repository} head`)
  return {
    repository,
    number: Number(number),
    state: 'open',
    base: { repository, ref: 'main', sha: baseSha },
    head: { repository: headRepository, sha: headSha },
    merge: { sha: mergeSha, parents: [baseSha, headSha] },
  }
}

export async function capturePairSnapshot({ dispatch, readPullRequest }) {
  const [producerPR, clientsPR] = await Promise.all([
    readPullRequest(pairedRepositories.producer, dispatch.producerNumber),
    readPullRequest(pairedRepositories.clients, dispatch.clientsNumber),
  ])
  const producer = pullRequestSnapshot(
    pairedRepositories.producer,
    dispatch.producerNumber,
    producerPR,
  )
  const clients = pullRequestSnapshot(pairedRepositories.clients, dispatch.clientsNumber, clientsPR)
  if (producer.head.sha !== dispatch.expectedProducerHeadSha)
    fail('live producer head does not match the operator-reviewed SHA')
  if (clients.head.sha !== dispatch.expectedClientsHeadSha)
    fail('live clients head does not match the operator-reviewed SHA')
  return { producer, clients }
}

export function assertPairSnapshotUnchanged(before, after) {
  for (const key of ['producer', 'clients']) {
    if (JSON.stringify(before[key]) !== JSON.stringify(after[key]))
      fail(`${key} pull request or merge snapshot changed during validation`)
  }
}

export function assertCheckoutMatchesSnapshot(snapshot, checkout) {
  if (requiredSha(checkout.revision, 'checked-out merge revision') !== snapshot.merge.sha)
    fail(`${snapshot.repository} checkout does not match the captured merge revision`)
  if (
    !Array.isArray(checkout.parents) ||
    JSON.stringify(checkout.parents) !== JSON.stringify(snapshot.merge.parents)
  )
    fail(`${snapshot.repository} merge revision does not have the captured base and head parents`)
}

export function shaIsValid(value) {
  return typeof value === 'string' && shaPattern.test(value)
}
