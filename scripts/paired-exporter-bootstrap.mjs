import { spawn } from 'node:child_process'
import { mkdir, open, lstat, readFile, realpath, symlink } from 'node:fs/promises'
import { join } from 'node:path'
import { pairedExporterPackages } from './paired-exporter-runtime-evidence.mjs'
import { collectPairedExporterOutput } from './paired-exporter-output-collector.mjs'
import { inspectPackageArchive, verifyPackageIntegrity } from './paired-exporter-archive.mjs'
import { prefixedFailure } from './prefixed-failure.mjs'
import { within } from './path-within.mjs'

const packagePins = pairedExporterPackages.map(({ name, version, integrity }) => [
  name,
  version,
  integrity,
])
const maximumArchiveBytes = 8 * 1024 * 1024
const maximumExpandedPackageBytes = 32 * 1024 * 1024
const maximumExpandedTotalBytes = 96 * 1024 * 1024

const fail = prefixedFailure('Trusted exporter bootstrap rejected input')

function packageFile(name, version) {
  return `${name.replaceAll('@', '').replaceAll('/', '_')}-${version}.tgz`
}

function validateManifest(manifest) {
  if (
    !manifest ||
    Object.keys(manifest).sort().join(',') !==
      'packages,producerLockBlob,producerRootHead,registryOrigin,schemaVersion' ||
    manifest.schemaVersion !== 1 ||
    manifest.registryOrigin !== 'https://registry.npmjs.org' ||
    !/^[a-f0-9]{40}$/u.test(manifest.producerLockBlob) ||
    !/^[a-f0-9]{40}$/u.test(manifest.producerRootHead) ||
    !Array.isArray(manifest.packages) ||
    manifest.packages.length !== packagePins.length
  )
    fail('dependency bundle manifest has an invalid shape')
  manifest.packages.forEach((entry, index) => {
    const [name, version, integrity] = packagePins[index]
    if (
      !entry ||
      Object.keys(entry).sort().join(',') !== 'file,integrity,name,size,version' ||
      entry.name !== name ||
      entry.version !== version ||
      entry.integrity !== integrity ||
      entry.file !== packageFile(name, version) ||
      !Number.isInteger(entry.size) ||
      entry.size < 1 ||
      entry.size > maximumArchiveBytes
    )
      fail(`dependency bundle entry ${index} does not match the reviewed package pin`)
  })
}

async function extractPackage(entry, archiveBytes) {
  try {
    verifyPackageIntegrity(archiveBytes, entry.integrity)
  } catch {
    fail(`package archive integrity mismatch for ${entry.name}`)
  }
  if (archiveBytes.length !== entry.size) fail(`package archive size mismatch for ${entry.name}`)
  const packageRoot = join('/node_modules', ...entry.name.split('/'))
  const packageRootReal = await realpath('/node_modules')
  await mkdir(packageRoot, { recursive: true })
  const extraction = inspectPackageArchive(archiveBytes, maximumExpandedPackageBytes)
  for (const member of extraction.entries) {
    const memberPath = member.name.split('/').slice(1)
    if (memberPath.length === 0) continue
    const target = join(packageRoot, ...memberPath)
    if (!within(packageRoot, target)) fail('package archive target escaped its package directory')
    if (member.type === '5') {
      await mkdir(target, { recursive: true })
      continue
    }
    await mkdir(join(target, '..'), { recursive: true })
    const handle = await open(target, 'wx', 0o444)
    try {
      await handle.writeFile(member.bytes)
    } finally {
      await handle.close()
    }
  }
  if (!(await lstat(packageRootReal)).isDirectory()) fail('isolated dependency root changed')
  return extraction.expandedBytes
}

async function installBundle() {
  const manifest = JSON.parse(await readFile('/packages/bundle.json', 'utf8'))
  validateManifest(manifest)
  let total = 0
  for (const entry of manifest.packages) {
    const archivePath = join('/packages', entry.file)
    const info = await lstat(archivePath)
    if (info.isSymbolicLink() || !info.isFile() || info.size !== entry.size)
      fail(`package bundle member is not a matching regular file: ${entry.file}`)
    if (info.size > maximumArchiveBytes) fail('package archive exceeds the compressed size limit')
    const archiveBytes = await readFile(archivePath)
    total += await extractPackage(entry, archiveBytes)
    if (total > maximumExpandedTotalBytes) fail('dependency bundle exceeds total expanded size')
  }
  await mkdir('/node_modules/@ts-shared', { recursive: true })
  await symlink('/producer/ts-shared/utils', '/node_modules/@ts-shared/utils')
}

async function runCandidate(script) {
  return await new Promise((resolve, reject) => {
    const child = spawn(
      process.execPath,
      [script, '--output-root', '/output', '--consumer-root', '/consumer'],
      {
        cwd: '/producer',
        env: { PATH: '/usr/local/bin:/usr/bin:/bin' },
        stdio: ['ignore', 'ignore', 'pipe'],
      },
    )
    let diagnosticBytes = 0
    const diagnostics = []
    let expired = false
    const timeout = setTimeout(
      () => {
        expired = true
        child.kill('SIGKILL')
      },
      5 * 60 * 1000,
    )
    child.stderr.on('data', chunk => {
      const remaining = 1024 * 1024 - diagnosticBytes
      if (remaining > 0) {
        const accepted = chunk.subarray(0, remaining)
        diagnostics.push(accepted)
        diagnosticBytes += accepted.length
      }
    })
    child.once('error', error => {
      clearTimeout(timeout)
      reject(error)
    })
    child.once('close', (code, signal) => {
      clearTimeout(timeout)
      if (expired) reject(new Error('candidate exporter timed out'))
      else if (code !== 0)
        reject(
          new Error(
            `candidate exporter failed (${signal ? `signal ${signal}` : `exit ${code}`}): ${
              Buffer.concat(diagnostics).toString('utf8').trim() || 'no diagnostic'
            }`,
          ),
        )
      else resolve()
    })
  })
}

await installBundle()
await runCandidate(process.argv[2])
const files = await collectPairedExporterOutput()
process.stdout.write(JSON.stringify({ schemaVersion: 1, files }))
