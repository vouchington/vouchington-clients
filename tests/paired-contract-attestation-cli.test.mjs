import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, mkdir, readFile, rename, rm, symlink, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { it } from 'node:test'
import { declaredFilamentsContractPaths } from '../scripts/contracts.mjs'
import {
  pairedExporterImage,
  pairedExporterPackages,
} from '../scripts/paired-exporter-runtime-evidence.mjs'
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
    assert.deepEqual(snapshot.pair.producer.merge.parents, ['a'.repeat(40), '1'.repeat(40)])
    assert.equal(snapshot.pair.producer.base.sha, '3'.repeat(40))
    assert.equal(snapshot.pair.producer.main.sha, 'b'.repeat(40))
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
        producer: { revision: '5'.repeat(40), parents: ['a'.repeat(40), '1'.repeat(40)] },
        clients: { revision: '6'.repeat(40), parents: ['a'.repeat(40), '2'.repeat(40)] },
        candidateConfig: config,
      }),
    )
    await writeFile(join(producerRoot, 'pnpm-lock.yaml'), 'lockfileVersion: "9.0"\n')
    const producerGit = args =>
      execFileSync('git', ['-C', producerRoot, ...args], { encoding: 'utf8' }).trim()
    producerGit(['init', '-q'])
    producerGit(['config', 'user.email', 'test@example.invalid'])
    producerGit(['config', 'user.name', 'Paired contract test'])
    producerGit(['add', '.'])
    producerGit(['commit', '-q', '-m', 'test candidate source'])
    const producerLockBlob = producerGit(['rev-parse', 'HEAD:pnpm-lock.yaml'])
    await writeFile(
      join(evidenceRoot, 'exporter-runtime.json'),
      JSON.stringify({
        schemaVersion: 1,
        producerRootHead: '5'.repeat(40),
        producerLockBlob,
        image: pairedExporterImage,
        packages: pairedExporterPackages.map(record => ({
          ...record,
          file: `${record.name.replaceAll('@', '').replaceAll('/', '_')}-${record.version}.tgz`,
          size: 1,
        })),
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
    const evidenceOriginal = join(temporaryRoot, 'evidence-original')
    await rename(evidenceRoot, evidenceOriginal)
    await symlink(evidenceOriginal, evidenceRoot)
    const evidenceSymlink = await runCli(
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
    assert.notEqual(evidenceSymlink.code, 0)
    assert.match(evidenceSymlink.stderr, /symbolic link found in candidate evidence/u)
    await rm(evidenceRoot)
    await rename(evidenceOriginal, evidenceRoot)

    const producerFixtures = join(producerRoot, 'api-fixtures')
    const producerFixturesOriginal = join(producerRoot, 'api-fixtures-original')
    const externalProducerFixtures = join(temporaryRoot, 'external-producer-fixtures')
    await rename(producerFixtures, producerFixturesOriginal)
    await mkdir(join(externalProducerFixtures, 'v1'), { recursive: true })
    await writeFile(join(externalProducerFixtures, 'v1/sample-0.txt'), 'sample-0\n')
    await symlink(externalProducerFixtures, producerFixtures)
    const producerSymlink = await runCli(
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
    assert.notEqual(producerSymlink.code, 0)
    assert.match(producerSymlink.stderr, /symbolic link found in fixture contract producer source/u)
    await rm(producerFixtures)
    await rename(producerFixturesOriginal, producerFixtures)

    const stageFixtures = join(stageRoot, 'api-fixtures')
    const stageFixturesOriginal = join(stageRoot, 'api-fixtures-original')
    const externalStageFixtures = join(temporaryRoot, 'external-stage-fixtures')
    await rename(stageFixtures, stageFixturesOriginal)
    await mkdir(join(externalStageFixtures, 'v1'), { recursive: true })
    await writeFile(join(externalStageFixtures, 'v1/sample-0.txt'), 'sample-0\n')
    await symlink(externalStageFixtures, stageFixtures)
    const stageSymlink = await runCli(
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
    assert.notEqual(stageSymlink.code, 0)
    assert.match(
      stageSymlink.stderr,
      /symbolic link found in (?:staged contract path api-fixtures\/v1|fixture contract staged tree)/u,
    )
    await rm(stageFixtures)
    await rename(stageFixturesOriginal, stageFixtures)

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

it('does not dispatch a CLI command when the attestation module is imported', async () => {
  const result = execFileSync(
    process.execPath,
    [
      '--input-type=module',
      '-e',
      `process.argv[2] = 'capture'; await import(${JSON.stringify(cli)});`,
    ],
    {
      cwd: fileURLToPath(new URL('..', import.meta.url)),
      encoding: 'utf8',
      env: { PATH: process.env.PATH ?? '/usr/bin:/bin' },
    },
  )
  assert.equal(result, '')
})
