import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { it } from 'node:test'

const workflowPath = '../.github/workflows/paired-native-contract-attestation.yml'
const readWorkflow = () => readFile(new URL(workflowPath, import.meta.url), 'utf8')

function jobBlock(workflow, name) {
  const marker = `  ${name}:\n`
  const start = workflow.indexOf(marker)
  assert.notEqual(start, -1, `missing job ${name}`)
  const remaining = workflow.slice(start + marker.length)
  const next = remaining.search(/\n  [a-z][\w-]*:\n/u)
  return workflow.slice(start, next === -1 ? undefined : start + marker.length + next)
}

function stepBlock(workflow, name) {
  const marker = `      - name: ${name}`
  const start = workflow.indexOf(marker)
  assert.notEqual(start, -1, `missing step ${name}`)
  const next = workflow.indexOf('\n      - name:', start + marker.length)
  return workflow.slice(start, next === -1 ? undefined : next)
}

it('keeps paired review evidence separate from the required native contract workflow', async () => {
  const [workflow, existingWorkflow] = await Promise.all([
    readWorkflow(),
    readFile(new URL('../.github/workflows/native-contract-tests.yml', import.meta.url), 'utf8'),
  ])
  assert.match(workflow, /^  workflow_dispatch:/mu)
  assert.doesNotMatch(workflow, /^  pull_request(?:_target)?:/mu)
  assert.doesNotMatch(workflow, /workflow_run:|createCheckRun|statuses\.create/u)
  assert.match(existingWorkflow, /^  pull_request:/mu)
  assert.match(existingWorkflow, /branches: \[main\]/u)
  assert.doesNotMatch(existingWorkflow, /paired-native-contract-attestation/u)
})

it('requires exact reviewed PR heads and explicit operator review', async () => {
  const workflow = await readWorkflow()
  for (const input of [
    'producer_pull_request:',
    'expected_producer_head_sha:',
    'clients_pull_request:',
    'expected_clients_head_sha:',
    'reviewed_both_snapshots:',
  ])
    assert.ok(workflow.includes(input), `missing dispatch input ${input}`)
  assert.match(workflow, /GITHUB_REF: \$\{\{ github\.ref \}\}/u)
  assert.match(workflow, /GITHUB_WORKFLOW_REF: \$\{\{ github\.workflow_ref \}\}/u)
  assert.match(workflow, /GITHUB_TRIGGERING_ACTOR: \$\{\{ github\.triggering_actor \}\}/u)
  assert.match(workflow, /GITHUB_RUN_ATTEMPT: \$\{\{ github\.run_attempt \}\}/u)
  assert.match(workflow, /permissions:\n  contents: read\n  pull-requests: read/u)
})

it('isolates candidate code from every authenticated identity-resolution step', async () => {
  const [workflow, isolatedRunner] = await Promise.all([
    readWorkflow(),
    readFile(new URL('../scripts/paired-exporter-runner.mjs', import.meta.url), 'utf8'),
  ])
  const candidate = jobBlock(workflow, 'candidate')
  assert.match(candidate, /needs: resolve/u)
  assert.match(candidate, /permissions:\n      contents: read\n/u)
  assert.doesNotMatch(candidate, /GITHUB_TOKEN:|github\.token|secrets\./u)
  for (const step of [
    'Pull the pinned isolated exporter runtime',
    'Fetch the exact reviewed exporter dependency bundle',
    'Verify candidate execution is isolated from runner credentials and host state',
    'Run the producer exporter in the isolated pinned runtime',
    'Stage the explicitly declared contract paths',
    'Assert extracted localization representatives',
    'Check paired candidate generated localization against staged contract',
  ])
    assert.match(candidate, new RegExp(`- name: ${step}`, 'u'))
  assert.doesNotMatch(candidate, /pnpm install|pnpm\/action-setup|npm install/u)
  assert.match(candidate, /docker pull --platform linux\/amd64/u)
  assert.match(candidate, /node:26\.12\.0-bookworm@sha256:[a-f0-9]{64}/u)
  assert.match(candidate, /run-isolated-native-exporter\.mjs/u)
  assert.match(isolatedRunner, /--cap-drop=ALL/u)
  assert.match(isolatedRunner, /--network=none/u)
  assert.match(isolatedRunner, /--read-only/u)
  assert.match(isolatedRunner, /--security-opt=no-new-privileges:true/u)
  assert.match(isolatedRunner, /--tmpfs[\s\S]*\/output:rw,noexec,nosuid,size=256m/u)
  assert.match(isolatedRunner, /--tmpfs[\s\S]*\/node_modules:rw,noexec,nosuid,size=128m/u)
  assert.match(isolatedRunner, /decodeCandidateOutput/u)
  assert.match(candidate, /fetch-paired-exporter-dependencies\.mjs/u)
  assert.doesNotMatch(candidate, /type=bind,src=\$\{EXPORTER_OUTPUT_ROOT\},dst=\/output/u)
  assert.match(jobBlock(workflow, 'resolve'), /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u)
  assert.match(jobBlock(workflow, 'finalize'), /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u)
  assert.match(jobBlock(workflow, 'finalize'), /needs: \[resolve, candidate\]/u)
  assert.match(jobBlock(workflow, 'finalize'), /actions\/download-artifact@[a-f0-9]{40}/u)
})

it('checks out fixed PR merge refs without persisting checkout credentials', async () => {
  const workflow = await readWorkflow()
  for (const [name, repository, refOutput] of [
    [
      'Check out the exact producer merge snapshot as untrusted data',
      'vouchington/vouchington',
      'producer_merge_ref',
    ],
    [
      'Check out the exact clients merge snapshot as untrusted data',
      'vouchington/vouchington-clients',
      'clients_merge_ref',
    ],
  ]) {
    const step = stepBlock(workflow, name)
    assert.match(step, new RegExp(`repository: ${repository.replaceAll('/', '\\/')}`, 'u'))
    assert.match(
      step,
      new RegExp(`ref: \\$\\{\\{ needs\\.resolve\\.outputs\\.${refOutput} \\}\\}`, 'u'),
    )
    assert.match(step, /fetch-depth: 0/u)
    assert.match(step, /persist-credentials: false/u)
    assert.match(step, /allow-unsafe-pr-checkout: true/u)
  }
  assert.doesNotMatch(workflow, /ref: \$\{\{ inputs\.(?:expected_|.*_ref)/u)
})

it('keeps candidate artifacts untrusted until a clean final job rechecks live identities', async () => {
  const workflow = await readWorkflow()
  const candidateUpload = stepBlock(
    workflow,
    'Upload untrusted candidate contract data for final validation',
  )
  const finalUpload = stepBlock(workflow, 'Upload informational paired contract evidence')
  const finalize = jobBlock(workflow, 'finalize')
  assert.match(candidateUpload, /name: paired-native-candidate-\$\{\{ github\.run_id \}\}/u)
  assert.match(candidateUpload, /retention-days: 1/u)
  assert.match(
    stepBlock(workflow, 'Recheck live identities and write exact informational evidence'),
    /GITHUB_TOKEN: \$\{\{ github\.token \}\}/u,
  )
  for (const step of [
    'Verify immutable merge snapshots and trusted contract declaration',
    'Assert extracted localization representatives from untrusted data',
    'Check exact candidate generated localization against untrusted data',
  ])
    assert.match(finalize, new RegExp(`- name: ${step}`, 'u'))
  assert.match(
    finalize,
    /PRODUCER_CANDIDATE_ROOT: \$\{\{ github\.workspace \}\}\/producer-candidate/u,
  )
  assert.ok(
    finalize.indexOf('Check exact candidate generated localization against untrusted data') <
      finalize.indexOf('Recheck live identities and write exact informational evidence'),
    'trusted candidate-data checks must precede the authenticated live recheck',
  )
  assert.match(finalUpload, /name: paired-native-contract-\$\{\{ github\.run_id \}\}/u)
  assert.match(finalUpload, /retention-days: 1/u)
  assert.doesNotMatch(finalUpload, /overwrite: true/u)
  assert.doesNotMatch(
    finalize,
    /run-isolated-native-exporter|native-localization\.mts|pnpm install/u,
  )
})
