import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdir, mkdtemp, readFile, rm, unlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { it } from 'node:test'
import { declaredFilamentsContractPaths } from '../scripts/contracts.mjs'
import { pairedRepositories } from '../scripts/paired-contract-policy.mjs'
import { finalizeNativeTestsCommand } from '../scripts/paired-native-test-finalize.mjs'
import { sha256 } from '../scripts/paired-native-test-evidence.mjs'
import { androidPhases } from '../scripts/paired-native-test-profiles.mjs'
import { startFakeGitHub } from './paired-contract-cli-test-support.mjs'
import { cleanupFixture } from './paired-native-cleanup-fixture.mjs'

function finalizerSnapshot(repository, number, baseSha, headSha, mergeSha) {
  return {
    repository,
    number,
    state: 'open',
    base: { repository, ref: 'main', sha: baseSha },
    head: { repository: `${repository}-fork`, sha: headSha },
    merge: { sha: mergeSha, parents: ['a'.repeat(40), headSha] },
    main: { sha: 'b'.repeat(40) },
  }
}

it('finalizes the CLI path from exact artifact API records, captured snapshots, APKs, and eight phases', async () => {
  const server = await startFakeGitHub()
  const root = await mkdtemp(join(tmpdir(), 'paired-native-finalizer-'))
  const evidenceRoot = join(root, 'contract')
  const apkRoot = join(root, 'apks')
  const runtimeRoot = join(root, 'runtime')
  await Promise.all([mkdir(evidenceRoot), mkdir(apkRoot), mkdir(runtimeRoot)])
  const producer = finalizerSnapshot(
    pairedRepositories.producer,
    3292,
    '3'.repeat(40),
    '1'.repeat(40),
    '5'.repeat(40),
  )
  const clients = finalizerSnapshot(
    pairedRepositories.clients,
    376,
    '4'.repeat(40),
    '2'.repeat(40),
    '6'.repeat(40),
  )
  const runId = '123456'
  const artifactDigests = {
    contract: 'a'.repeat(64),
    apks: 'b'.repeat(64),
    runtime: 'c'.repeat(64),
  }
  const workflow = {
    repository: pairedRepositories.clients,
    ref: 'refs/heads/main',
    workflowRef: `${pairedRepositories.clients}/.github/workflows/paired-native-contract-attestation.yml@refs/heads/main`,
    sha: 'f'.repeat(40),
    runId,
    runAttempt: 1,
    name: 'Paired native contract review evidence',
  }
  const files = declaredFilamentsContractPaths
    .map((path, index) => ({
      path: `${path}/fixture-${index}.json`,
      sha256: createHash('sha256').update(`fixture-${index}`).digest('hex'),
      sizeBytes: 10,
    }))
    .sort((left, right) => left.path.localeCompare(right.path))
  const manifest = {
    schemaVersion: 1,
    attestation: {
      actor: 'operator',
      actorPermission: 'maintain',
      reviewedBothSnapshots: true,
      expectedProducerHeadSha: producer.head.sha,
      expectedClientsHeadSha: clients.head.sha,
      workflow,
    },
    producer,
    clients,
    contract: {
      outputOrigin: 'untrusted-candidate-exporter',
      repository: pairedRepositories.producer,
      ref: 'main',
      declaredPaths: declaredFilamentsContractPaths,
      files,
    },
  }
  const nativeTestProfile = 'pending-turn-recovery'
  const pairSnapshot = {
    dispatch: { nativeTestProfile },
    pair: { producer, clients },
  }
  const app = Buffer.from('app candidate')
  const testApk = Buffer.from('test candidate')
  await writeFile(join(evidenceRoot, 'paired-attestation.json'), JSON.stringify(manifest))
  await writeFile(join(apkRoot, 'app-debug.apk'), app)
  await writeFile(join(apkRoot, 'app-debug-androidTest.apk'), testApk)
  await writeFile(join(apkRoot, 'candidate-revision.txt'), `${clients.merge.sha}\n`)
  await writeFile(
    join(apkRoot, 'sha256sums.txt'),
    `${sha256(app)}  app-debug.apk\n${sha256(testApk)}  app-debug-androidTest.apk\n`,
  )
  await writeFile(
    join(runtimeRoot, 'runtime-result.json'),
    JSON.stringify({
      schema: 1,
      runId,
      runAttempt: 1,
      nativeTestProfile,
      clientsMergeSha: clients.merge.sha,
      apkSha256: sha256(app),
      emulator: {
        version: '37.2.12.0',
        api: 35,
        abis: ['x86_64', 'arm64-v8a'],
        nativeBridge: 'libndk_translation.so',
        buildFingerprint:
          'google/sdk_gphone64_x86_64/emu64x:15/AB1A.240505.005/1234567:userdebug/test-keys',
      },
      phases: androidPhases.map(method => ({ method, exitCode: 0, tests: 1 })),
      cleanup: cleanupFixture({
        runId,
        nativeTestProfile,
        clientsMergeSha: clients.merge.sha,
      }),
    }),
  )

  const artifactRecords = [
    {
      id: 101,
      digest: `sha256:${artifactDigests.contract}`,
      name: `paired-native-contract-${runId}-1`,
    },
    { id: 102, digest: `sha256:${artifactDigests.apks}`, name: `paired-native-apks-${runId}-1` },
    {
      id: 103,
      digest: `sha256:${artifactDigests.runtime}`,
      name: `paired-native-runtime-${runId}-1`,
    },
  ].map(item => ({ ...item, expired: false, workflow_run: { id: Number(runId), run_attempt: 1 } }))
  const env = {
    GITHUB_API_URL: server.apiURL,
    GITHUB_TOKEN: 'test-token',
    GITHUB_REPOSITORY: pairedRepositories.clients,
    GITHUB_EVENT_NAME: 'workflow_dispatch',
    GITHUB_REF: 'refs/heads/main',
    GITHUB_WORKFLOW_REF: workflow.workflowRef,
    GITHUB_SHA: workflow.sha,
    GITHUB_WORKFLOW: workflow.name,
    GITHUB_ACTOR: 'operator',
    GITHUB_TRIGGERING_ACTOR: 'operator',
    GITHUB_RUN_ID: runId,
    GITHUB_RUN_ATTEMPT: '1',
    INPUT_PRODUCER_PULL_REQUEST: '3292',
    INPUT_EXPECTED_PRODUCER_HEAD_SHA: producer.head.sha,
    INPUT_CLIENTS_PULL_REQUEST: '376',
    INPUT_EXPECTED_CLIENTS_HEAD_SHA: clients.head.sha,
    INPUT_NATIVE_TEST_PROFILE: nativeTestProfile,
    INPUT_REVIEWED_BOTH_SNAPSHOTS: 'true',
    NATIVE_LINUX_RESULT: 'success',
    NATIVE_SWIFT_CORE_MACOS_RESULT: 'success',
    NATIVE_ANDROID_BUILD_RESULT: 'success',
    NATIVE_ANDROID_EMULATOR_RESULT: 'success',
    CONTRACT_ARTIFACT_ID: '101',
    CONTRACT_ARTIFACT_DIGEST: artifactDigests.contract,
    CONTRACT_ARTIFACT_NAME: `paired-native-contract-${runId}-1`,
    APK_ARTIFACT_ID: '102',
    APK_ARTIFACT_DIGEST: artifactDigests.apks,
    APK_ARTIFACT_NAME: `paired-native-apks-${runId}-1`,
    RUNTIME_ARTIFACT_ID: '103',
    RUNTIME_ARTIFACT_DIGEST: artifactDigests.runtime,
    RUNTIME_ARTIFACT_NAME: `paired-native-runtime-${runId}-1`,
    PAIRED_EVIDENCE_ROOT: evidenceRoot,
    APK_BUNDLE_ROOT: apkRoot,
    RUNTIME_RESULT_ROOT: runtimeRoot,
    PAIR_SNAPSHOT_BASE64: Buffer.from(JSON.stringify(pairSnapshot)).toString('base64url'),
  }
  const saved = new Map(Object.keys(env).map(key => [key, process.env[key]]))
  const originalFetch = globalThis.fetch
  Object.assign(process.env, env)
  globalThis.fetch = async (input, options) => {
    if (String(input).includes(`/actions/runs/${runId}/artifacts`))
      return { ok: true, json: async () => ({ artifacts: artifactRecords }) }
    return originalFetch(input, options)
  }
  try {
    await finalizeNativeTestsCommand()
    const output = JSON.parse(
      await readFile(join(evidenceRoot, 'paired-native-test-evidence.json'), 'utf8'),
    )
    assert.equal(output.classification, 'untrusted-candidate-execution-informational')
    assert.equal(output.nativeTestProfile, nativeTestProfile)
    assert.equal(output.androidRuntime.phases.length, androidPhases.length)
    assert.equal(output.androidRuntime.cleanup.avdName, `paired-native-${runId}`)
    assert.equal(output.livePairRechecked, true)
    process.env.INPUT_NATIVE_TEST_PROFILE = 'cookie-snapshot'
    await assert.rejects(
      finalizeNativeTestsCommand(),
      /differs from the captured operator dispatch/u,
    )
    process.env.INPUT_NATIVE_TEST_PROFILE = nativeTestProfile
    const changedManifest = structuredClone(manifest)
    changedManifest.producer.base.sha = 'c'.repeat(40)
    await writeFile(join(evidenceRoot, 'paired-attestation.json'), JSON.stringify(changedManifest))
    await unlink(join(evidenceRoot, 'paired-native-test-evidence.json'))
    await assert.rejects(finalizeNativeTestsCommand(), /snapshots differ from the captured/u)
    const runtimeResult = JSON.parse(
      await readFile(join(runtimeRoot, 'runtime-result.json'), 'utf8'),
    )
    runtimeResult.cleanup.runId = '999'
    await writeFile(join(runtimeRoot, 'runtime-result.json'), JSON.stringify(runtimeResult))
    await writeFile(join(evidenceRoot, 'paired-attestation.json'), JSON.stringify(manifest))
    await assert.rejects(
      finalizeNativeTestsCommand(),
      /cleanup evidence is bound to another workflow run/u,
    )
  } finally {
    globalThis.fetch = originalFetch
    for (const [key, value] of saved)
      if (value === undefined) delete process.env[key]
      else process.env[key] = value
    await server.close()
    await rm(root, { recursive: true, force: true })
  }
})
