import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { it } from 'node:test'
import { declaredFilamentsContractPaths } from '../scripts/contracts.mjs'
import {
  dispatchEnvironment,
  runCli,
  startFakeGitHub,
} from './paired-contract-cli-test-support.mjs'

const cli = fileURLToPath(new URL('../scripts/paired-contract-attestation.mjs', import.meta.url))

it('wires capture and create-manifest CLIs through live identity checks', async () => {
  const server = await startFakeGitHub()
  const temporaryRoot = await mkdtemp(join(tmpdir(), 'paired-contract-cli-'))
  try {
    const captureOutput = join(temporaryRoot, 'capture-output')
    const capture = await runCli(
      cli,
      'capture',
      fileURLToPath(new URL('..', import.meta.url)),
      dispatchEnvironment(server.apiURL, captureOutput),
    )
    assert.equal(capture.code, 0, capture.stderr)
    const output = await readFile(captureOutput, 'utf8')
    const encodedSnapshot = output.match(/^pair_snapshot_base64=(.+)$/mu)?.[1]
    assert.ok(encodedSnapshot)
    const snapshot = JSON.parse(Buffer.from(encodedSnapshot, 'base64url').toString('utf8'))
    assert.equal(snapshot.pair.producer.merge.sha, '5'.repeat(40))
    assert.equal(snapshot.dispatch.clientsPermission, 'maintain')

    const trustedRoot = join(temporaryRoot, 'trusted')
    const evidenceRoot = join(temporaryRoot, 'evidence')
    const stageRoot = join(evidenceRoot, 'contracts')
    const producerRoot = join(temporaryRoot, 'producer-candidate')
    await mkdir(trustedRoot)
    await mkdir(evidenceRoot)
    await mkdir(producerRoot)
    const configDirectory = join(trustedRoot, 'contracts')
    await mkdir(configDirectory)
    await writeFile(
      join(configDirectory, 'filaments.json'),
      JSON.stringify({
        schemaVersion: 1,
        repository: 'vouchington/vouchington',
        ref: 'main',
        paths: declaredFilamentsContractPaths,
      }),
    )
    for (const [index, contractPath] of declaredFilamentsContractPaths.entries()) {
      const directory = join(stageRoot, ...contractPath.split('/'))
      await mkdir(directory, { recursive: true })
      await writeFile(join(directory, `sample-${index}.txt`), `sample-${index}\n`)
      if (index === 0) {
        const producerDirectory = join(producerRoot, ...contractPath.split('/'))
        await mkdir(producerDirectory, { recursive: true })
        await writeFile(join(producerDirectory, `sample-${index}.txt`), `sample-${index}\n`)
      }
    }
    const config = {
      schemaVersion: 1,
      repository: 'vouchington/vouchington',
      ref: 'main',
      paths: declaredFilamentsContractPaths,
    }
    await writeFile(
      join(evidenceRoot, 'candidate-evidence.json'),
      JSON.stringify({
        producer: { revision: '5'.repeat(40), parents: ['3'.repeat(40), '1'.repeat(40)] },
        clients: { revision: '6'.repeat(40), parents: ['4'.repeat(40), '2'.repeat(40)] },
        candidateConfig: config,
      }),
    )
    const git = args => execFileSync('git', args, { cwd: trustedRoot, encoding: 'utf8' }).trim()
    git(['init', '-q'])
    git(['config', 'user.email', 'test@example.invalid'])
    git(['config', 'user.name', 'Paired contract test'])
    git(['add', 'contracts/filaments.json'])
    git(['commit', '-q', '-m', 'test trusted declaration'])
    const trustedSha = git(['rev-parse', 'HEAD'])
    const manifest = await runCli(
      cli,
      'create-manifest',
      trustedRoot,
      dispatchEnvironment(server.apiURL, captureOutput, {
        GITHUB_SHA: trustedSha,
        GITHUB_WORKFLOW: 'Paired native contract review evidence',
        GITHUB_RUN_ID: '123456',
        PAIR_SNAPSHOT_BASE64: encodedSnapshot,
        PAIRED_EVIDENCE_ROOT: evidenceRoot,
        CONTRACT_STAGE_ROOT: stageRoot,
        PRODUCER_CANDIDATE_ROOT: producerRoot,
        TRUSTED_CLIENTS_ROOT: trustedRoot,
      }),
    )
    assert.equal(manifest.code, 0, manifest.stderr)
    const manifestPath = join(evidenceRoot, 'paired-attestation.json')
    const result = JSON.parse(await readFile(manifestPath, 'utf8'))
    assert.equal(result.contract.outputOrigin, 'untrusted-candidate-exporter')
    assert.equal(result.contract.files.length, declaredFilamentsContractPaths.length)
    assert.ok(result.contract.files.every(file => file.sha256.length === 64))

    await rm(manifestPath)
    await writeFile(join(stageRoot, 'api-fixtures/v1/sample-0.txt'), 'tampered\n')
    const tampered = await runCli(
      cli,
      'create-manifest',
      trustedRoot,
      dispatchEnvironment(server.apiURL, captureOutput, {
        GITHUB_SHA: trustedSha,
        GITHUB_WORKFLOW: 'Paired native contract review evidence',
        GITHUB_RUN_ID: '123456',
        PAIR_SNAPSHOT_BASE64: encodedSnapshot,
        PAIRED_EVIDENCE_ROOT: evidenceRoot,
        CONTRACT_STAGE_ROOT: stageRoot,
        PRODUCER_CANDIDATE_ROOT: producerRoot,
        TRUSTED_CLIENTS_ROOT: trustedRoot,
      }),
    )
    assert.notEqual(tampered.code, 0)
    assert.match(tampered.stderr, /staged file differs from producer source/u)
  } finally {
    await server.close()
    await rm(temporaryRoot, { recursive: true, force: true })
  }
})
