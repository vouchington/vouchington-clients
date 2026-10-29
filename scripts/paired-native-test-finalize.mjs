import { join } from 'node:path'
import { lstat, readFile, writeFile } from 'node:fs/promises'
import { assertPairSnapshotUnchanged } from './paired-contract-policy.mjs'
import { captureLivePair } from './paired-contract-github.mjs'
import { bindArtifact, listRegularFiles, verifyApkBundle } from './paired-native-test-evidence.mjs'
import { validateCleanupEvidence } from './paired-native-cleanup-evidence.mjs'
import { validateInstrumentationResult } from './paired-native-test-runtime-result.mjs'
import { validatePairManifest } from './paired-contract-manifest-validation.mjs'
import { assertCheckoutMatchesSnapshot } from './paired-contract-policy.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'

const fail = prefixedFailure('Paired native test finalization failed')

function required(value, label) {
  if (typeof value !== 'string' || value.trim() === '') fail(`${label} is required`)
  return value.trim()
}

async function readArtifactList() {
  const token = required(process.env.GITHUB_TOKEN, 'GitHub token')
  const api = (process.env.GITHUB_API_URL ?? 'https://api.github.com').replace(/\/$/u, '')
  const repository = required(process.env.GITHUB_REPOSITORY, 'workflow repository')
  const runId = required(process.env.GITHUB_RUN_ID, 'workflow run ID')
  const response = await fetch(
    `${api}/repos/${repository}/actions/runs/${runId}/artifacts?per_page=100`,
    {
      headers: {
        Accept: 'application/vnd.github+json',
        Authorization: `Bearer ${token}`,
        'X-GitHub-Api-Version': '2022-11-28',
      },
    },
  )
  if (!response.ok) fail(`artifact API request failed (${response.status})`)
  const body = await response.json()
  if (!Array.isArray(body.artifacts)) fail('artifact API response is invalid')
  return body.artifacts
}

async function readJsonFile(path, label) {
  const info = await lstat(path)
  if (!info.isFile() || info.isSymbolicLink()) fail(`${label} must be a regular file`)
  return JSON.parse(await readFile(path, 'utf8'))
}

export async function finalizeNativeTestsCommand() {
  const runId = required(process.env.GITHUB_RUN_ID, 'workflow run ID')
  const runAttempt = Number(required(process.env.GITHUB_RUN_ATTEMPT, 'workflow run attempt'))
  if (runAttempt !== 1) fail('rerun attempts are not accepted')
  const nativeTestProfile = required(process.env.INPUT_NATIVE_TEST_PROFILE, 'native test profile')
  for (const job of [
    'NATIVE_LINUX_RESULT',
    'NATIVE_SWIFT_CORE_MACOS_RESULT',
    'NATIVE_ANDROID_BUILD_RESULT',
    'NATIVE_ANDROID_EMULATOR_RESULT',
  ])
    if (process.env[job] !== 'success') fail(`${job} did not succeed`)
  const expectedArtifacts = [
    {
      id: Number(required(process.env.CONTRACT_ARTIFACT_ID, 'contract artifact ID')),
      digest: required(process.env.CONTRACT_ARTIFACT_DIGEST, 'contract artifact digest'),
      name: required(process.env.CONTRACT_ARTIFACT_NAME, 'contract artifact name'),
    },
    {
      id: Number(required(process.env.APK_ARTIFACT_ID, 'APK artifact ID')),
      digest: required(process.env.APK_ARTIFACT_DIGEST, 'APK artifact digest'),
      name: required(process.env.APK_ARTIFACT_NAME, 'APK artifact name'),
    },
    {
      id: Number(required(process.env.RUNTIME_ARTIFACT_ID, 'runtime artifact ID')),
      digest: required(process.env.RUNTIME_ARTIFACT_DIGEST, 'runtime artifact digest'),
      name: required(process.env.RUNTIME_ARTIFACT_NAME, 'runtime artifact name'),
    },
  ]
  if (
    new Set(expectedArtifacts.map(item => item.name)).size !== expectedArtifacts.length ||
    new Set(expectedArtifacts.map(item => item.id)).size !== expectedArtifacts.length
  )
    fail('artifact names or IDs are duplicated')
  const artifacts = await readArtifactList()
  let runtimeUpload
  for (const expected of expectedArtifacts) {
    const found = artifacts.filter(item => item.name === expected.name)
    if (found.length !== 1) fail(`expected exactly one artifact named ${expected.name}`)
    bindArtifact({ artifact: found[0], expected, runId, runAttempt })
    if (expected.name === process.env.RUNTIME_ARTIFACT_NAME) runtimeUpload = found[0]
  }
  const evidenceRoot = required(process.env.PAIRED_EVIDENCE_ROOT, 'evidence root')
  const contractManifest = await readJsonFile(
    join(evidenceRoot, 'paired-attestation.json'),
    'paired contract manifest',
  )
  if (!validatePairManifest(contractManifest)) fail('paired contract manifest failed validation')
  if (
    contractManifest.attestation.workflow.runId !== runId ||
    contractManifest.attestation.workflow.runAttempt !== runAttempt
  )
    fail('contract manifest belongs to another workflow run or attempt')
  const apkRoot = required(process.env.APK_BUNDLE_ROOT, 'APK bundle root')
  const runtimeRoot = required(process.env.RUNTIME_RESULT_ROOT, 'runtime result root')
  const runtimeFiles = await listRegularFiles(runtimeRoot, { maxFiles: 1, maxDepth: 0 })
  if (JSON.stringify([...runtimeFiles.keys()]) !== JSON.stringify(['runtime-result.json']))
    fail('runtime artifact file set differs from the fixed allowlist')
  const result = JSON.parse(runtimeFiles.get('runtime-result.json').toString('utf8'))
  const encodedSnapshot = required(process.env.PAIR_SNAPSHOT_BASE64, 'captured pair snapshot')
  const snapshotBytes = Buffer.from(encodedSnapshot, 'base64url')
  if (snapshotBytes.toString('base64url') !== encodedSnapshot)
    fail('captured pair snapshot encoding is invalid')
  const pairSnapshot = JSON.parse(snapshotBytes.toString('utf8'))
  if (pairSnapshot.dispatch?.nativeTestProfile !== nativeTestProfile)
    fail('native test profile differs from the captured operator dispatch')
  if (
    JSON.stringify(contractManifest.producer) !== JSON.stringify(pairSnapshot.pair.producer) ||
    JSON.stringify(contractManifest.clients) !== JSON.stringify(pairSnapshot.pair.clients)
  )
    fail('contract manifest snapshots differ from the captured reviewed pair')
  assertCheckoutMatchesSnapshot(pairSnapshot.pair.producer, {
    revision: contractManifest.producer.merge.sha,
    parents: contractManifest.producer.merge.parents,
  })
  assertCheckoutMatchesSnapshot(pairSnapshot.pair.clients, {
    revision: contractManifest.clients.merge.sha,
    parents: contractManifest.clients.merge.parents,
  })
  const apkHashes = await verifyApkBundle(apkRoot, contractManifest.clients.merge.sha)
  validateInstrumentationResult(result, {
    runId,
    nativeTestProfile,
    clientsMergeSha: contractManifest.clients.merge.sha,
    apkSha256: apkHashes['app-debug.apk'],
  })
  const runtimeExpected = expectedArtifacts.find(
    item => item.name === process.env.RUNTIME_ARTIFACT_NAME,
  )
  const runtimeDigest = /^sha256:([0-9a-f]{64})$/u.exec(runtimeUpload?.digest ?? '')
  validateCleanupEvidence(result.cleanup, {
    runId,
    runAttempt,
    nativeTestProfile,
    clientsMergeSha: contractManifest.clients.merge.sha,
    runtimeArtifactId: runtimeExpected?.id,
    runtimeArtifactDigest: runtimeExpected?.digest,
    boundRuntimeArtifactId: runtimeUpload?.id,
    boundRuntimeArtifactDigest: runtimeDigest?.[1],
  })
  if (result.runAttempt !== runAttempt)
    fail('runtime evidence attempt differs from the workflow attempt')
  const { pair: livePair } = await captureLivePair()
  assertPairSnapshotUnchanged(pairSnapshot.pair, livePair)
  const output = {
    schema: 1,
    classification: 'untrusted-candidate-execution-informational',
    runId,
    runAttempt,
    reviewedPair: pairSnapshot.pair,
    artifacts: expectedArtifacts,
    nativeTestProfile,
    nativeTests: {
      swiftCoreLinux: 'success',
      dotnetCoreLinux: 'success',
      swiftCoreMacOS: 'success',
    },
    androidBuild: { status: 'success', apkSha256: apkHashes },
    androidRuntime: result,
    livePairRechecked: true,
  }
  await writeFile(
    join(evidenceRoot, 'paired-native-test-evidence.json'),
    `${JSON.stringify(output, null, 2)}\n`,
    { flag: 'wx' },
  )
}
