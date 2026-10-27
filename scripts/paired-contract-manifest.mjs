import { lstat, readFile, readdir } from 'node:fs/promises'
import { join } from 'node:path'
import { declaredFilamentsContractPaths } from './contracts.mjs'
import { pairedRepositories, assertCheckoutMatchesSnapshot } from './paired-contract-policy.mjs'
import { validatePairManifest } from './paired-contract-manifest-validation.mjs'
import { sha256 } from './sha256.mjs'

function fail(message) {
  throw new Error(`Paired contract attestation failed: ${message}`)
}

async function realDirectory(path, label) {
  let stat
  try {
    stat = await lstat(path)
  } catch (error) {
    fail(`cannot read ${label}: ${error.message}`)
  }
  if (!stat.isDirectory() || stat.isSymbolicLink()) fail(`${label} must be a real directory`)
}

async function listFiles(root, declaredPaths) {
  for (const declaredPath of declaredPaths) {
    let info
    try {
      info = await lstat(join(root, ...declaredPath.split('/')))
    } catch (error) {
      fail(`missing declared staged path ${declaredPath}: ${error.message}`)
    }
    if (!info.isDirectory() || info.isSymbolicLink())
      fail(`declared staged path ${declaredPath} must be a real directory`)
  }
  const files = []
  async function walk(directory, prefix = '') {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name)
      const relativePath = prefix ? `${prefix}/${entry.name}` : entry.name
      if (entry.isSymbolicLink()) fail(`symbolic link found in staged contract at ${relativePath}`)
      const allowed = declaredPaths.some(
        item =>
          item === relativePath ||
          item.startsWith(`${relativePath}/`) ||
          relativePath.startsWith(`${item}/`),
      )
      if (!allowed) fail(`unexpected staged contract path ${relativePath}`)
      if (entry.isDirectory()) await walk(path, relativePath)
      else if (entry.isFile()) {
        const bytes = await readFile(path)
        files.push({
          path: relativePath,
          sha256: sha256(bytes),
          sizeBytes: bytes.length,
        })
      } else fail(`unsupported staged contract entry ${relativePath}`)
    }
  }
  await walk(root)
  files.sort((left, right) => (left.path < right.path ? -1 : left.path > right.path ? 1 : 0))
  if (files.length === 0) fail('staged contract contains no files')
  return files
}

export async function createPairManifest({
  dispatch,
  pair,
  checkoutRevisions,
  contractConfig,
  stageRoot,
  workflow,
}) {
  assertCheckoutMatchesSnapshot(pair.producer, checkoutRevisions.producer)
  assertCheckoutMatchesSnapshot(pair.clients, checkoutRevisions.clients)
  await realDirectory(stageRoot, 'staged contract root')
  const paths = contractConfig?.paths
  if (
    contractConfig?.schemaVersion !== 1 ||
    contractConfig?.repository !== pairedRepositories.producer ||
    contractConfig?.ref !== 'main' ||
    !Array.isArray(paths) ||
    JSON.stringify(Object.keys(contractConfig).sort()) !==
      JSON.stringify(['paths', 'ref', 'repository', 'schemaVersion']) ||
    JSON.stringify(paths) !== JSON.stringify(declaredFilamentsContractPaths)
  )
    fail('trusted contract configuration is invalid')
  const files = await listFiles(stageRoot, paths)
  return validatePairManifest({
    schemaVersion: 1,
    attestation: {
      actor: dispatch.actor,
      actorPermission: dispatch.clientsPermission,
      reviewedBothSnapshots: true,
      expectedProducerHeadSha: dispatch.expectedProducerHeadSha,
      expectedClientsHeadSha: dispatch.expectedClientsHeadSha,
      workflow,
    },
    producer: pair.producer,
    clients: pair.clients,
    contract: {
      outputOrigin: 'untrusted-candidate-exporter',
      repository: contractConfig.repository,
      ref: contractConfig.ref,
      declaredPaths: [...paths],
      files,
    },
  })
}
