import { execFileSync } from 'node:child_process'
import { createManifestCommand } from './paired-contract-finalize.mjs'
import { join, resolve } from 'node:path'
import { lstat, readFile, writeFile } from 'node:fs/promises'
import { assertCheckoutMatchesSnapshot } from './paired-contract-policy.mjs'
import { captureLivePair } from './paired-contract-github.mjs'

function fail(message) {
  throw new Error(`Paired contract attestation failed: ${message}`)
}

function requiredString(value, label) {
  if (typeof value !== 'string' || value.trim() === '') fail(`${label} is required`)
  return value.trim()
}

function requiredSha(value, label) {
  if (typeof value !== 'string' || !/^[0-9a-f]{40}$/u.test(value))
    fail(`${label} must be a lowercase 40-character commit SHA`)
  return value
}

async function readJson(path, label) {
  try {
    const info = await lstat(path)
    if (!info.isFile() || info.isSymbolicLink()) fail(`${label} must be a regular file`)
    return JSON.parse(await readFile(path, 'utf8'))
  } catch (error) {
    fail(`cannot read ${label}: ${error.message}`)
  }
}

function git(root, args) {
  return execFileSync('git', ['-C', root, ...args], { encoding: 'utf8' }).trim()
}

function checkoutIdentity(root) {
  const revision = git(root, ['rev-parse', 'HEAD'])
  const parents = git(root, ['rev-list', '--parents', '-n', '1', 'HEAD']).split(/\s+/u).slice(1)
  const status = git(root, ['status', '--porcelain', '--untracked-files=all'])
  if (status !== '') fail(`candidate checkout is not clean: ${root}`)
  return { revision, parents }
}

function decodeSnapshot() {
  const encoded = requiredString(process.env.PAIR_SNAPSHOT_BASE64, 'captured pair snapshot')
  const bytes = Buffer.from(encoded, 'base64url')
  if (bytes.toString('base64url') !== encoded) fail('captured pair snapshot encoding is invalid')
  try {
    return JSON.parse(bytes.toString('utf8'))
  } catch (error) {
    fail(`captured pair snapshot is invalid: ${error.message}`)
  }
}

async function captureCommand() {
  const captured = await captureLivePair()
  const outputPath = requiredString(process.env.GITHUB_OUTPUT, 'GitHub output path')
  const outputs = [
    `pair_snapshot_base64=${Buffer.from(JSON.stringify(captured)).toString('base64url')}`,
    `producer_number=${captured.pair.producer.number}`,
    `producer_merge_ref=refs/pull/${captured.pair.producer.number}/merge`,
    `clients_number=${captured.pair.clients.number}`,
    `clients_merge_ref=refs/pull/${captured.pair.clients.number}/merge`,
  ]
  await writeFile(outputPath, `${outputs.join('\n')}\n`, { flag: 'a' })
}

async function materializeSnapshotCommand() {
  const captured = decodeSnapshot()
  const snapshotPath = resolve(
    requiredString(process.env.PAIR_SNAPSHOT_PATH, 'snapshot output path'),
  )
  await writeFile(snapshotPath, `${JSON.stringify(captured, null, 2)}\n`, { flag: 'wx' })
}

async function trustedConfiguration(trustedRoot, candidateRoot) {
  const trustedConfig = await readJson(
    join(trustedRoot, 'contracts/filaments.json'),
    'trusted contract configuration',
  )
  const candidateConfig = await readJson(
    join(candidateRoot, 'contracts/filaments.json'),
    'candidate contract configuration',
  )
  if (JSON.stringify(candidateConfig) !== JSON.stringify(trustedConfig))
    fail('candidate contract configuration differs from the trusted main declaration')
  return { trustedConfig, candidateConfig }
}

async function verifyCheckoutsCommand() {
  const snapshot = await readJson(process.env.PAIR_SNAPSHOT_PATH, 'captured pair snapshot')
  const producerRoot = resolve(
    requiredString(process.env.PRODUCER_CANDIDATE_ROOT, 'producer candidate root'),
  )
  const clientsRoot = resolve(
    requiredString(process.env.CLIENTS_CANDIDATE_ROOT, 'clients candidate root'),
  )
  const trustedRoot = resolve(
    requiredString(process.env.TRUSTED_CLIENTS_ROOT, 'trusted clients root'),
  )
  const producer = checkoutIdentity(producerRoot)
  const clients = checkoutIdentity(clientsRoot)
  assertCheckoutMatchesSnapshot(snapshot.pair.producer, producer)
  assertCheckoutMatchesSnapshot(snapshot.pair.clients, clients)
  const trustedRevision = git(trustedRoot, ['rev-parse', 'HEAD'])
  const expectedTrustedRevision = requiredSha(process.env.GITHUB_SHA, 'trusted workflow revision')
  if (trustedRevision !== expectedTrustedRevision)
    fail('trusted clients checkout differs from the workflow revision')
  await trustedConfiguration(trustedRoot, clientsRoot)
}

async function recordCandidateCommand() {
  const snapshot = await readJson(process.env.PAIR_SNAPSHOT_PATH, 'captured pair snapshot')
  const producerRoot = resolve(
    requiredString(process.env.PRODUCER_CANDIDATE_ROOT, 'producer candidate root'),
  )
  const clientsRoot = resolve(
    requiredString(process.env.CLIENTS_CANDIDATE_ROOT, 'clients candidate root'),
  )
  const trustedRoot = resolve(
    requiredString(process.env.TRUSTED_CLIENTS_ROOT, 'trusted clients root'),
  )
  if (
    git(trustedRoot, ['rev-parse', 'HEAD']) !==
    requiredSha(process.env.GITHUB_SHA, 'trusted workflow revision')
  )
    fail('trusted clients checkout changed during candidate validation')
  const producer = checkoutIdentity(producerRoot)
  const clients = checkoutIdentity(clientsRoot)
  assertCheckoutMatchesSnapshot(snapshot.pair.producer, producer)
  assertCheckoutMatchesSnapshot(snapshot.pair.clients, clients)
  const { candidateConfig } = await trustedConfiguration(trustedRoot, clientsRoot)
  const evidenceRoot = resolve(requiredString(process.env.PAIRED_EVIDENCE_ROOT, 'evidence root'))
  await writeFile(
    join(evidenceRoot, 'candidate-evidence.json'),
    `${JSON.stringify({ producer, clients, candidateConfig }, null, 2)}\n`,
    { flag: 'wx' },
  )
}

export async function runPairedContractCommand(command) {
  if (command === 'capture') await captureCommand()
  else if (command === 'materialize-snapshot') await materializeSnapshotCommand()
  else if (command === 'verify-checkouts') await verifyCheckoutsCommand()
  else if (command === 'record-candidate') await recordCandidateCommand()
  else if (command === 'create-manifest') await createManifestCommand()
  else fail(`unknown command ${command}`)
}
