import { isAbsolute, join, relative, resolve, sep } from 'node:path'
import { lstat, readFile, readdir, writeFile } from 'node:fs/promises'
import { decodeSnapshot, git, readJson } from './paired-contract-command-support.mjs'
import {
  pairedRepositories,
  assertCheckoutMatchesSnapshot,
  assertPairSnapshotUnchanged,
  fail,
  requiredSha,
  requiredString,
} from './paired-contract-policy.mjs'
import { createPairManifest } from './paired-contract-manifest.mjs'
import { validatePairedExporterRuntimeEvidence } from './paired-exporter-runtime-evidence.mjs'
import { captureLivePair } from './paired-contract-github.mjs'
import { declaredFilamentsContractPaths } from './contracts.mjs'

async function assertNoSymlinkComponents(root, target, label) {
  const pathFromRoot = relative(root, target)
  if (pathFromRoot.split(sep).includes('..') || isAbsolute(pathFromRoot))
    fail(`${label} is outside its trusted root`)

  let current = root
  const components = pathFromRoot === '' ? [] : pathFromRoot.split(sep)
  for (let index = -1; index < components.length; index += 1) {
    if (index >= 0) current = join(current, components[index])
    const info = await lstat(current)
    if (info.isSymbolicLink()) fail(`symbolic link found in ${label} at ${current}`)
    if (index < components.length - 1 && !info.isDirectory())
      fail(`${label} contains a non-directory path component at ${current}`)
  }
}

async function listRegularFiles(root, label) {
  const rootInfo = await lstat(root)
  if (!rootInfo.isDirectory() || rootInfo.isSymbolicLink())
    fail(`${label} must be a real directory`)
  const files = new Map()
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name
      const info = await lstat(path)
      if (info.isSymbolicLink()) fail(`symbolic link found in ${label} at ${relativePath}`)
      if (info.isDirectory()) await walk(path, relativePath)
      else if (info.isFile()) files.set(relativePath, await readFile(path))
      else fail(`unsupported entry in ${label} at ${relativePath}`)
    }
  }
  await walk(root)
  if (files.size === 0) fail(`${label} contains no files`)
  return files
}

async function assertDirectoryTreesEqual(leftRoot, rightRoot, label, leftBase, rightBase) {
  await Promise.all([
    assertNoSymlinkComponents(leftBase, leftRoot, `${label} staged tree`),
    assertNoSymlinkComponents(rightBase, rightRoot, `${label} producer source`),
  ])
  const [left, right] = await Promise.all([
    listRegularFiles(leftRoot, `${label} staged tree`),
    listRegularFiles(rightRoot, `${label} producer source`),
  ])
  if (left.size !== right.size) fail(`${label} staged tree differs from producer source`)
  for (const [path, bytes] of left) {
    if (!right.has(path) || !bytes.equals(right.get(path)))
      fail(`${label} staged file differs from producer source: ${path}`)
  }
}

export async function createManifestCommand() {
  const captured = decodeSnapshot()
  const evidenceRoot = resolve(requiredString(process.env.PAIRED_EVIDENCE_ROOT, 'evidence root'))
  const candidateEvidencePath = join(evidenceRoot, 'candidate-evidence.json')
  await assertNoSymlinkComponents(evidenceRoot, candidateEvidencePath, 'candidate evidence')
  const candidateEvidence = await readJson(candidateEvidencePath, 'candidate checkout evidence')
  const { dispatch, pair: after } = await captureLivePair()
  assertPairSnapshotUnchanged(captured.pair, after)
  const trustedRoot = resolve(
    requiredString(process.env.TRUSTED_CLIENTS_ROOT, 'trusted clients root'),
  )
  if (
    git(trustedRoot, ['rev-parse', 'HEAD']) !==
    requiredSha(process.env.GITHUB_SHA, 'trusted workflow revision')
  )
    fail('trusted clients checkout changed during validation')
  if (git(trustedRoot, ['status', '--porcelain', '--untracked-files=all']) !== '')
    fail(`trusted checkout changed during validation: ${trustedRoot}`)
  const contractConfig = await readJson(
    join(trustedRoot, 'contracts/filaments.json'),
    'trusted contract configuration',
  )
  if (JSON.stringify(candidateEvidence.candidateConfig) !== JSON.stringify(contractConfig))
    fail('candidate contract configuration differs from the trusted main declaration')
  assertCheckoutMatchesSnapshot(captured.pair.producer, candidateEvidence.producer)
  assertCheckoutMatchesSnapshot(captured.pair.clients, candidateEvidence.clients)
  const workflow = {
    repository: requiredString(process.env.GITHUB_REPOSITORY, 'workflow repository'),
    ref: requiredString(process.env.GITHUB_REF, 'workflow ref'),
    workflowRef: requiredString(process.env.GITHUB_WORKFLOW_REF, 'workflow definition ref'),
    sha: requiredSha(process.env.GITHUB_SHA, 'workflow SHA'),
    runId: requiredString(process.env.GITHUB_RUN_ID, 'workflow run ID'),
    runAttempt: Number(requiredString(process.env.GITHUB_RUN_ATTEMPT, 'workflow run attempt')),
    name: requiredString(process.env.GITHUB_WORKFLOW, 'workflow name'),
  }
  if (
    workflow.repository !== pairedRepositories.clients ||
    workflow.ref !== 'refs/heads/main' ||
    workflow.workflowRef !==
      `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main` ||
    workflow.runAttempt !== 1
  )
    fail('workflow identity changed during validation')
  const contractRoot = resolve(
    requiredString(process.env.CONTRACT_STAGE_ROOT, 'contract stage root'),
  )
  const producerRoot = resolve(
    requiredString(process.env.PRODUCER_CANDIDATE_ROOT, 'producer candidate root'),
  )
  const exporterRuntimePath = join(evidenceRoot, 'exporter-runtime.json')
  await assertNoSymlinkComponents(evidenceRoot, exporterRuntimePath, 'exporter runtime evidence')
  const exporterRuntime = await readJson(exporterRuntimePath, 'exporter runtime evidence')
  const producerLockBlob = git(producerRoot, ['rev-parse', 'HEAD:pnpm-lock.yaml'])
  validatePairedExporterRuntimeEvidence(exporterRuntime, {
    producerHead: candidateEvidence.producer.revision,
    lockBlob: producerLockBlob,
  })
  for (const contractPath of declaredFilamentsContractPaths)
    await assertNoSymlinkComponents(
      contractRoot,
      join(contractRoot, ...contractPath.split('/')),
      `staged contract path ${contractPath}`,
    )
  await assertDirectoryTreesEqual(
    join(contractRoot, ...declaredFilamentsContractPaths[0].split('/')),
    join(producerRoot, ...declaredFilamentsContractPaths[0].split('/')),
    'fixture contract',
    contractRoot,
    producerRoot,
  )
  const manifest = await createPairManifest({
    dispatch,
    pair: captured.pair,
    checkoutRevisions: candidateEvidence,
    contractConfig,
    stageRoot: contractRoot,
    workflow,
  })
  await writeFile(
    join(evidenceRoot, 'paired-attestation.json'),
    `${JSON.stringify(manifest, null, 2)}\n`,
    { flag: 'wx' },
  )
}
