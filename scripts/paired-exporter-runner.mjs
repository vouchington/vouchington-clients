import { randomUUID } from 'node:crypto'
import { lstat, readdir, readFile, writeFile } from 'node:fs/promises'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  pairedExporterImage,
  validatePairedExporterRuntimeEvidence,
} from './paired-exporter-runtime-evidence.mjs'
import { runDocker, removeOwnedContainer } from './paired-exporter-container.mjs'
import { directory, overlaps } from './paired-exporter-paths.mjs'
import {
  assertUnprivilegedHostIdentity,
  decodeCandidateOutput,
  materializeOutput,
  maximumResponseBytes,
} from './paired-exporter-output-protocol.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'

const image = pairedExporterImage.reference
const bootstrap = fileURLToPath(new URL('./paired-exporter-bootstrap.mjs', import.meta.url))
const runtimeContract = fileURLToPath(
  new URL('./paired-exporter-runtime-evidence.mjs', import.meta.url),
)
const archiveValidator = fileURLToPath(new URL('./paired-exporter-archive.mjs', import.meta.url))
const outputCollector = fileURLToPath(
  new URL('./paired-exporter-output-collector.mjs', import.meta.url),
)
const maximumDiagnosticBytes = 4 * 1024 * 1024
const defaultExecutionTimeoutMs = 5 * 60 * 1000
const maximumExecutionTimeoutMs = defaultExecutionTimeoutMs

const fail = prefixedFailure('Isolated native exporter failed')

export async function runIsolatedNativeExporter({
  producerRoot,
  consumerRoot,
  outputRoot,
  packageBundleRoot,
  runtimeEvidencePath,
  executionTimeoutMs = defaultExecutionTimeoutMs,
}) {
  if (
    !Number.isSafeInteger(executionTimeoutMs) ||
    executionTimeoutMs < 1 ||
    executionTimeoutMs > maximumExecutionTimeoutMs
  )
    fail('candidate execution timeout is outside the allowed bound')
  const [producer, consumer, output, bundle] = await Promise.all([
    directory(producerRoot, 'producer root'),
    directory(consumerRoot, 'consumer root'),
    directory(outputRoot, 'export output root'),
    directory(packageBundleRoot, 'dependency bundle root'),
  ])
  if (producer === consumer || overlaps(producer, consumer) || overlaps(consumer, producer))
    fail('producer and consumer roots must be distinct')
  if ([producer, consumer].some(root => overlaps(root, output) || overlaps(output, root)))
    fail('export output root must be isolated from source roots')
  if ([producer, consumer, output].some(root => overlaps(root, bundle) || overlaps(bundle, root)))
    fail('dependency bundle root must be isolated from source and output roots')
  if ((await readdir(output)).length !== 0) fail('export output root must be empty')
  const uid = typeof process.getuid === 'function' ? process.getuid() : undefined
  const gid = typeof process.getgid === 'function' ? process.getgid() : undefined
  assertUnprivilegedHostIdentity(uid, gid)
  const runtimeBytes = await runDocker(
    [
      'run',
      '--pull=never',
      '--rm',
      '--platform=linux/amd64',
      '--network=none',
      '--read-only',
      '--user=65534:65534',
      '--cap-drop=ALL',
      '--security-opt=no-new-privileges:true',
      '--pids-limit=8',
      '--memory=128m',
      '--cpus=0.5',
      image,
      'sh',
      '-c',
      'node --version && git --version',
    ],
    { timeoutMs: 30_000, stdoutLimit: 1024, stderrLimit: 4096 },
  )
  const [nodeVersion, gitVersion, ...extraVersionLines] = runtimeBytes
    .toString('utf8')
    .trim()
    .split('\n')
  if (
    nodeVersion !== pairedExporterImage.nodeVersion ||
    gitVersion !== pairedExporterImage.gitVersion ||
    extraVersionLines.length !== 0
  )
    fail('runtime image executable versions differ from the reviewed image')
  const runtime = pairedExporterImage
  const containerName = `paired-native-export-${process.pid}-${randomUUID()}`
  const args = [
    'run',
    '--pull=never',
    '--name',
    containerName,
    '--platform=linux/amd64',
    '--network=none',
    '--read-only',
    '--user',
    `${uid}:${gid}`,
    '--cap-drop=ALL',
    '--security-opt=no-new-privileges:true',
    '--pids-limit=128',
    '--memory=2g',
    '--cpus=2',
    '--tmpfs',
    `/tmp:rw,noexec,nosuid,size=64m,uid=${uid},gid=${gid},mode=0700`,
    '--tmpfs',
    `/output:rw,noexec,nosuid,size=256m,uid=${uid},gid=${gid},mode=0700`,
    '--tmpfs',
    `/node_modules:rw,noexec,nosuid,size=128m,uid=${uid},gid=${gid},mode=0700`,
    '--mount',
    `type=bind,src=${producer},dst=/producer,readonly`,
    '--mount',
    `type=bind,src=${consumer},dst=/consumer,readonly`,
    '--mount',
    `type=bind,src=${bundle},dst=/packages,readonly`,
    '--mount',
    `type=bind,src=${bootstrap},dst=/trusted/bootstrap.mjs,readonly`,
    '--mount',
    `type=bind,src=${runtimeContract},dst=/trusted/paired-exporter-runtime-evidence.mjs,readonly`,
    '--mount',
    `type=bind,src=${archiveValidator},dst=/trusted/paired-exporter-archive.mjs,readonly`,
    '--mount',
    `type=bind,src=${outputCollector},dst=/trusted/paired-exporter-output-collector.mjs,readonly`,
    image,
    'node',
    '/trusted/bootstrap.mjs',
    '/producer/dev/native-localization.mts',
  ]
  let bytes
  let executionError
  try {
    bytes = await runDocker(args, {
      timeoutMs: executionTimeoutMs,
      stdoutLimit: maximumResponseBytes,
      stderrLimit: maximumDiagnosticBytes,
    })
  } catch (error) {
    executionError = error
  }
  let cleanupError
  try {
    await removeOwnedContainer(containerName)
  } catch (error) {
    cleanupError = error
  }
  if (executionError) {
    const cleanupDetail = cleanupError ? `; ${cleanupError.message}` : ''
    fail(`${executionError.message}${cleanupDetail}`)
  }
  if (cleanupError) fail(cleanupError.message)
  const files = decodeCandidateOutput(bytes)
  await materializeOutput(output, files)
  if (runtimeEvidencePath !== undefined) {
    const suppliedEvidencePath = resolve(runtimeEvidencePath)
    const evidenceParent = await directory(dirname(suppliedEvidencePath), 'runtime evidence parent')
    const evidencePath = join(evidenceParent, basename(suppliedEvidencePath))
    try {
      await lstat(evidencePath)
      fail('runtime evidence output already exists')
    } catch (error) {
      if (error.code !== 'ENOENT') throw error
    }
    if ([producer, consumer, output, bundle].some(root => overlaps(root, evidencePath)))
      fail('runtime evidence output must be outside source and data roots')
    const dependencyManifest = JSON.parse(await readFile(join(bundle, 'bundle.json'), 'utf8'))
    const evidence = validatePairedExporterRuntimeEvidence(
      {
        schemaVersion: 1,
        producerRootHead: dependencyManifest.producerRootHead,
        producerLockBlob: dependencyManifest.producerLockBlob,
        image: runtime,
        packages: dependencyManifest.packages,
      },
      {
        producerHead: dependencyManifest.producerRootHead,
        lockBlob: dependencyManifest.producerLockBlob,
      },
    )
    await writeFile(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`, {
      flag: 'wx',
      mode: 0o600,
    })
  }
  return {
    image,
    platform: 'linux/amd64',
    files: files.length,
    bytes: files.reduce((sum, file) => sum + file.contents.length, 0),
  }
}
