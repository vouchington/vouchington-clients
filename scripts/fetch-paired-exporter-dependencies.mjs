import { execFile } from 'node:child_process'
import { lstat, readFile, realpath, readdir, writeFile } from 'node:fs/promises'
import { join, relative, sep, isAbsolute } from 'node:path'
import { promisify } from 'node:util'
import { packageIntegrity } from './paired-exporter-archive.mjs'
import { packagePins, verifyPairedExporterLock } from './paired-exporter-lock-validation.mjs'
export { verifyPairedExporterLock } from './paired-exporter-lock-validation.mjs'

const execFileAsync = promisify(execFile)
const registryOrigin = 'https://registry.npmjs.org'
const maximumPackageBytes = 8 * 1024 * 1024
const maximumBundleBytes = 24 * 1024 * 1024
const timeoutMs = 120_000

function fail(message) {
  throw new Error(`Paired exporter dependency bundle rejected: ${message}`)
}

function safeName(name) {
  return name.replaceAll('@', '').replaceAll('/', '_')
}

function isWithin(root, path) {
  const child = relative(root, path)
  return child !== '' && child !== '..' && !child.startsWith(`..${sep}`) && !isAbsolute(child)
}

async function realDirectory(value, label) {
  const supplied = await lstat(value)
  if (supplied.isSymbolicLink() || !supplied.isDirectory())
    fail(`${label} must be a real directory`)
  const path = await realpath(value)
  const resolved = await lstat(path)
  if (resolved.isSymbolicLink() || !resolved.isDirectory())
    fail(`${label} must be a real directory`)
  return path
}

async function regularFile(root, path, label) {
  let current = root
  for (const part of path.split('/')) {
    current = join(current, part)
    const info = await lstat(current)
    if (info.isSymbolicLink()) fail(`${label} contains a symbolic link`)
  }
  const resolved = await realpath(current)
  if (!isWithin(root, resolved)) fail(`${label} escapes the producer root`)
  const info = await lstat(resolved)
  if (!info.isFile()) fail(`${label} must be a regular file`)
  return resolved
}

async function readGitBlob(root) {
  const { stdout } = await execFileAsync('git', ['-C', root, 'rev-parse', 'HEAD:pnpm-lock.yaml'], {
    timeout: 10_000,
    maxBuffer: 64 * 1024,
    env: { PATH: process.env.PATH ?? '/usr/bin:/bin', GIT_CONFIG_NOSYSTEM: '1' },
  })
  const blob = stdout.trim()
  if (!/^[a-f0-9]{40}$/u.test(blob)) fail('could not bind the lockfile to a Git blob')
  return blob
}

export async function readPinnedRegistryBytes(pin, fetchImpl = fetch) {
  const filename = `${pin.name.split('/').at(-1)}-${pin.version}.tgz`
  const encodedName = pin.name.startsWith('@') ? pin.name.replace('/', '%2f') : pin.name
  const url = `${registryOrigin}/${encodedName}/-/${filename}`
  const response = await fetchImpl(url, {
    redirect: 'error',
    signal: AbortSignal.timeout(timeoutMs),
    headers: { accept: 'application/octet-stream' },
  })
  if (!response.ok)
    fail(`registry request failed for ${pin.name}@${pin.version}: HTTP ${response.status}`)
  if (new URL(response.url).origin !== registryOrigin)
    fail('registry response came from an unexpected origin')
  const length = Number(response.headers.get('content-length'))
  if (Number.isFinite(length) && length > maximumPackageBytes)
    fail(`registry response exceeds size limit for ${pin.name}@${pin.version}`)
  const chunks = []
  let size = 0
  for await (const chunk of response.body) {
    size += chunk.length
    if (size > maximumPackageBytes)
      fail(`registry response exceeds size limit for ${pin.name}@${pin.version}`)
    chunks.push(chunk)
  }
  const body = Buffer.concat(chunks, size)
  if (packageIntegrity(body) !== pin.integrity)
    fail(`registry bytes do not match reviewed integrity for ${pin.name}@${pin.version}`)
  return body
}

export async function fetchPairedExporterDependencies({ producerRoot, outputRoot }) {
  const producer = await realDirectory(producerRoot, 'producer root')
  const output = await realDirectory(outputRoot, 'bundle output root')
  if (producer === output || isWithin(producer, output) || isWithin(output, producer))
    fail('bundle output must be isolated from producer source')
  if ((await readdir(output)).length !== 0) fail('bundle output root must be empty')
  const packageJsonPath = await regularFile(
    producer,
    'ts-shared/ui-messages/package.json',
    'exporter package manifest',
  )
  const lockPath = await regularFile(producer, 'pnpm-lock.yaml', 'pnpm lockfile')
  const packageJson = JSON.parse(await readFile(packageJsonPath, 'utf8'))
  const lock = await readFile(lockPath, 'utf8')
  verifyPairedExporterLock(lock, packageJson)
  const lockBlob = await readGitBlob(producer)
  let total = 0
  const entries = []
  for (const pin of packagePins) {
    const bytes = await readPinnedRegistryBytes(pin)
    total += bytes.length
    if (total > maximumBundleBytes) fail('dependency bundle exceeds total size limit')
    const file = `${safeName(pin.name)}-${pin.version}.tgz`
    await writeFile(join(output, file), bytes, { flag: 'wx', mode: 0o600 })
    entries.push({
      name: pin.name,
      version: pin.version,
      integrity: pin.integrity,
      file,
      size: bytes.length,
    })
  }
  const manifest = {
    schemaVersion: 1,
    registryOrigin,
    producerLockBlob: lockBlob,
    producerRootHead: (
      await execFileAsync('git', ['-C', producer, 'rev-parse', 'HEAD'], { timeout: 10_000 })
    ).stdout.trim(),
    packages: entries,
  }
  await writeFile(join(output, 'bundle.json'), `${JSON.stringify(manifest, null, 2)}\n`, {
    flag: 'wx',
    mode: 0o600,
  })
  return manifest
}

function parseArguments(argv) {
  if (argv.length !== 4 || argv[0] !== '--producer-root' || argv[2] !== '--output-root')
    fail(
      'usage: node scripts/fetch-paired-exporter-dependencies.mjs --producer-root <root> --output-root <root>',
    )
  return { producerRoot: argv[1], outputRoot: argv[3] }
}

if (import.meta.main) await fetchPairedExporterDependencies(parseArguments(process.argv.slice(2)))
