import {
  pairedRepositories,
  capturePairSnapshot,
  validateDispatch,
} from './paired-contract-policy.mjs'

function fail(message) {
  throw new Error(`Paired contract attestation failed: ${message}`)
}

function requiredString(value, label) {
  if (typeof value !== 'string' || value.trim() === '') fail(`${label} is required`)
  return value.trim()
}

async function githubRequest(path, { authenticated = false } = {}) {
  const apiURL = requiredString(
    process.env.GITHUB_API_URL ?? 'https://api.github.com',
    'GitHub API URL',
  )
  const headers = {
    Accept: 'application/vnd.github+json',
    'X-GitHub-Api-Version': '2022-11-28',
  }
  if (authenticated)
    headers.Authorization = `Bearer ${requiredString(process.env.GITHUB_TOKEN, 'GitHub API token')}`
  const response = await fetch(`${apiURL.replace(/\/$/u, '')}/${path}`, {
    headers,
  })
  if (!response.ok) fail(`GitHub API request failed (${response.status}) for ${path}`)
  return response.json()
}

export async function captureLivePair() {
  const staticDispatch = {
    eventName: process.env.GITHUB_EVENT_NAME,
    repository: process.env.GITHUB_REPOSITORY,
    ref: process.env.GITHUB_REF,
    workflowRef: process.env.GITHUB_WORKFLOW_REF,
    runAttempt: process.env.GITHUB_RUN_ATTEMPT,
    actor: process.env.GITHUB_ACTOR,
    triggeringActor: process.env.GITHUB_TRIGGERING_ACTOR,
    inputs: {
      clientsPullRequest: process.env.INPUT_CLIENTS_PULL_REQUEST,
      producerPullRequest: process.env.INPUT_PRODUCER_PULL_REQUEST,
      expectedClientsHeadSha: process.env.INPUT_EXPECTED_CLIENTS_HEAD_SHA,
      expectedProducerHeadSha: process.env.INPUT_EXPECTED_PRODUCER_HEAD_SHA,
      reviewedBothSnapshots: process.env.INPUT_REVIEWED_BOTH_SNAPSHOTS,
    },
    clientsPermission: 'admin',
  }
  validateDispatch(staticDispatch)
  const permissionResponse = await githubRequest(
    `repos/${pairedRepositories.clients}/collaborators/${encodeURIComponent(requiredString(process.env.GITHUB_ACTOR, 'workflow actor'))}/permission`,
    { authenticated: true },
  )
  const permission = normalizeMaintainerPermission(permissionResponse)
  const dispatch = validateDispatch({ ...staticDispatch, clientsPermission: permission })
  const readPullRequest = async (repository, number) =>
    githubRequest(`repos/${repository}/pulls/${number}`, { authenticated: true })
  const pair = await capturePairSnapshot({ dispatch, readPullRequest })
  return { dispatch, pair }
}

export function normalizeMaintainerPermission(response) {
  if (!response || typeof response !== 'object')
    fail('GitHub collaborator permission response is invalid')
  const { permission, role_name: roleName, permissions } = response
  // GitHub reports the base permission separately from custom repository roles.
  // A maintainer can therefore have permission="write" while role_name is
  // "maintain". Accept only the documented built-in role with corroborating
  // permissions; custom roles and inconsistent responses fail closed.
  if (
    roleName === 'admin' &&
    permission === 'admin' &&
    (permissions === undefined || permissions?.admin === true)
  )
    return 'admin'
  if (
    roleName === 'maintain' &&
    permission === 'write' &&
    (permissions === undefined || (permissions.maintain === true && permissions.admin === false))
  )
    return 'maintain'
  fail('workflow actor must have a built-in maintain or admin repository role')
}
