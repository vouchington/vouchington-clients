import { spawn } from 'node:child_process'
import { createServer } from 'node:http'

const clientsRepository = 'vouchington/vouchington-clients'
const producerRepository = 'vouchington/vouchington'
const sha = character => character.repeat(40)

function pullRequest(repository, number, base, head, merge) {
  return {
    number,
    state: 'open',
    base: { repo: { full_name: repository }, ref: 'main', sha: base },
    head: { repo: { full_name: `${repository}-fork` }, sha: head },
    merge_commit_sha: merge,
  }
}

export async function startFakeGitHub() {
  const pullRequests = new Map([
    [
      `/repos/${producerRepository}/pulls/3292`,
      pullRequest(producerRepository, 3292, sha('3'), sha('1'), sha('5')),
    ],
    [
      `/repos/${clientsRepository}/pulls/376`,
      pullRequest(clientsRepository, 376, sha('4'), sha('2'), sha('6')),
    ],
  ])
  const server = createServer((request, response) => {
    response.setHeader('content-type', 'application/json')
    if (request.url === `/repos/${clientsRepository}/collaborators/operator/permission`) {
      response.end(
        JSON.stringify({
          permission: 'write',
          role_name: 'maintain',
          permissions: { pull: true, triage: true, push: true, maintain: true, admin: false },
        }),
      )
      return
    }
    const snapshot = pullRequests.get(request.url)
    if (snapshot) {
      response.end(JSON.stringify(snapshot))
      return
    }
    response.statusCode = 404
    response.end(JSON.stringify({ message: 'unexpected route' }))
  })
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
  const address = server.address()
  return {
    apiURL: `http://127.0.0.1:${address.port}`,
    close: () =>
      new Promise((resolve, reject) => server.close(error => (error ? reject(error) : resolve()))),
  }
}

export function dispatchEnvironment(apiURL, outputPath, extra = {}) {
  return {
    ...process.env,
    GITHUB_API_URL: apiURL,
    GITHUB_TOKEN: 'test-token',
    GITHUB_EVENT_NAME: 'workflow_dispatch',
    GITHUB_REPOSITORY: clientsRepository,
    GITHUB_REF: 'refs/heads/main',
    GITHUB_WORKFLOW_REF: `${clientsRepository}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main`,
    GITHUB_RUN_ATTEMPT: '1',
    GITHUB_ACTOR: 'operator',
    GITHUB_TRIGGERING_ACTOR: 'operator',
    INPUT_PRODUCER_PULL_REQUEST: '3292',
    INPUT_EXPECTED_PRODUCER_HEAD_SHA: sha('1'),
    INPUT_CLIENTS_PULL_REQUEST: '376',
    INPUT_EXPECTED_CLIENTS_HEAD_SHA: sha('2'),
    INPUT_REVIEWED_BOTH_SNAPSHOTS: 'true',
    GITHUB_OUTPUT: outputPath,
    ...extra,
  }
}

export function runCli(cli, command, cwd, env) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [cli, command], {
      cwd,
      env,
      stdio: ['ignore', 'pipe', 'pipe'],
    })
    let stdout = ''
    let stderr = ''
    child.stdout.setEncoding('utf8').on('data', chunk => (stdout += chunk))
    child.stderr.setEncoding('utf8').on('data', chunk => (stderr += chunk))
    child.once('error', reject)
    child.once('close', code => resolve({ code, stdout, stderr }))
  })
}
